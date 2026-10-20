using HexFund.UI.Services.Http;

namespace HexFund.UI.Tests;

public class RetryAfterParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 14, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("120", 120)]
    [InlineData(" 90 ", 90)]
    public void Seconds_are_parsed(string value, int expectedSeconds) =>
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), RetryAfterParser.Parse(value, Now));

    [Fact]
    public void An_http_date_is_converted_to_a_delay()
    {
        var value = Now.AddMinutes(5).ToString("R");

        Assert.Equal(TimeSpan.FromMinutes(5), RetryAfterParser.Parse(value, Now));
    }

    [Fact]
    public void A_date_in_the_past_gives_zero()
    {
        var value = Now.AddMinutes(-5).ToString("R");

        Assert.Equal(TimeSpan.Zero, RetryAfterParser.Parse(value, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("-5")]
    [InlineData("1.5")]
    public void Missing_or_unreadable_values_give_null(string? value) =>
        Assert.Null(RetryAfterParser.Parse(value, Now));

    [Fact]
    public void A_huge_delay_is_capped_at_seven_days() =>
        Assert.Equal(TimeSpan.FromDays(7), RetryAfterParser.Parse("99999999999", Now));
}

public class PathSanitizerTests
{
    [Fact]
    public void Query_string_is_removed() =>
        Assert.Equal("accounts/{id}/monthly-overview",
            PathSanitizer.Sanitize("accounts/3fa85f64-5717-4562-b3fc-2c963f66afa6/monthly-overview?year=2026&month=10"));

    [Fact]
    public void Guid_segments_become_id_placeholders() =>
        Assert.Equal("accounts/{id}/transactions/{id}",
            PathSanitizer.Sanitize("accounts/3fa85f64-5717-4562-b3fc-2c963f66afa6/transactions/B1C7AAAA-1111-2222-3333-444455556666"));

    [Fact]
    public void Numeric_segments_become_id_placeholders() =>
        Assert.Equal("accounts/{id}/transactions", PathSanitizer.Sanitize("/accounts/42/transactions"));

    [Fact]
    public void Absolute_urls_lose_the_host_and_the_api_prefix() =>
        Assert.Equal("categories/{id}", PathSanitizer.Sanitize(
            "https://example.azurewebsites.net/api/categories/3fa85f64-5717-4562-b3fc-2c963f66afa6?x=1"));

    [Fact]
    public void Fragments_are_removed() =>
        Assert.Equal("accounts", PathSanitizer.Sanitize("accounts#top"));

    [Fact]
    public void Words_with_digits_are_kept() =>
        Assert.Equal("transactions-for-date", PathSanitizer.Sanitize("transactions-for-date?date=2026-10-20"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Empty_input_gives_empty_string(string? value) =>
        Assert.Equal(string.Empty, PathSanitizer.Sanitize(value));

    [Fact]
    public void Very_long_paths_are_cut()
    {
        var result = PathSanitizer.Sanitize(new string('a', 500));

        Assert.Equal(PathSanitizer.MaxLength, result.Length);
    }
}

public class FailureBufferTests
{
    private static FailureRecord Record(int i) =>
        new(DateTimeOffset.UnixEpoch.AddSeconds(i), "GET", $"p{i}", 500, null, null);

    [Fact]
    public void Keeps_records_oldest_first()
    {
        var buffer = new FailureBuffer();
        for (var i = 0; i < 3; i++) buffer.Add(Record(i));

        Assert.Equal(new[] { "p0", "p1", "p2" }, buffer.Snapshot().Select(r => r.Path));
    }

    [Fact]
    public void Drops_the_oldest_beyond_the_default_capacity_of_20()
    {
        var buffer = new FailureBuffer();
        for (var i = 0; i < 25; i++) buffer.Add(Record(i));

        var snapshot = buffer.Snapshot();

        Assert.Equal(20, snapshot.Count);
        Assert.Equal("p5", snapshot[0].Path);
        Assert.Equal("p24", snapshot[^1].Path);
    }

    [Fact]
    public void Snapshot_is_a_copy()
    {
        var buffer = new FailureBuffer();
        buffer.Add(Record(0));
        var snapshot = buffer.Snapshot();

        buffer.Add(Record(1));

        Assert.Single(snapshot);
    }

    [Fact]
    public void Clear_empties_the_buffer()
    {
        var buffer = new FailureBuffer();
        buffer.Add(Record(0));

        buffer.Clear();

        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public void Capacity_must_be_positive() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new FailureBuffer(0));

    [Fact]
    public async Task Concurrent_adds_never_exceed_the_capacity()
    {
        var buffer = new FailureBuffer(10);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < 200; i++) buffer.Add(Record(t * 1000 + i));
        })));

        Assert.Equal(10, buffer.Snapshot().Count);
    }
}
