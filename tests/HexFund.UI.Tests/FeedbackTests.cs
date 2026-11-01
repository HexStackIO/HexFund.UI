using System.Text.Json;
using HexFund.UI.Models;
using HexFund.UI.Services;
using HexFund.UI.Services.Http;
using HexFund.UI.Validation;

namespace HexFund.UI.Tests;

public class FeedbackValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("      ")]
    public void Empty_or_whitespace_is_rejected(string? body) =>
        Assert.NotNull(UIValidator.ValidateFeedbackBody(body));

    [Theory]
    [InlineData("123456789", false)]            // 9 characters
    [InlineData("1234567890", true)]            // 10 characters
    [InlineData("  1234567890  ", true)]        // trimmed first
    [InlineData("   123456789   ", false)]
    public void Minimum_length_is_10_after_trimming(string body, bool valid) =>
        Assert.Equal(valid, UIValidator.ValidateFeedbackBody(body) is null);

    [Fact]
    public void Maximum_length_is_4000_after_trimming()
    {
        Assert.Null(UIValidator.ValidateFeedbackBody(new string('a', 4000)));
        Assert.Null(UIValidator.ValidateFeedbackBody(" " + new string('a', 4000) + " "));
        Assert.NotNull(UIValidator.ValidateFeedbackBody(new string('a', 4001)));
    }

    [Fact]
    public void Punctuation_and_newlines_are_allowed()
    {
        const string body = "Crash when I tap <Save>;\nSELECT * FROM x -- see /* log */";

        Assert.Null(UIValidator.ValidateFeedbackBody(body));
    }
}

public class FeedbackOutcomeTests
{
    [Fact]
    public void Success_clears_the_draft_and_says_the_report_was_received()
    {
        var outcome = FeedbackOutcomes.From(ApiResult.Success(202));

        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.KeepDraft);
        Assert.Equal("Thanks, your report was received.", outcome.Message);
    }

    [Theory]
    [InlineData(1, "about 1 minute.")]
    [InlineData(30, "about 1 minute.")]
    [InlineData(60, "about 1 minute.")]
    [InlineData(61, "about 2 minutes.")]
    [InlineData(3600, "about 60 minutes.")]
    public void Rate_limit_rounds_minutes_up(int seconds, string expectedEnding)
    {
        var result = ApiResult.Failure(ApiResultKind.RateLimited, 429, retryAfter: TimeSpan.FromSeconds(seconds));

        var outcome = FeedbackOutcomes.From(result);

        Assert.StartsWith("You have sent several reports recently.", outcome.Message);
        Assert.EndsWith(expectedEnding, outcome.Message);
        Assert.True(outcome.KeepDraft);
    }

    [Fact]
    public void Rate_limit_without_retry_after_asks_to_try_later()
    {
        var outcome = FeedbackOutcomes.From(ApiResult.Failure(ApiResultKind.RateLimited, 429));

        Assert.EndsWith("Please try again later.", outcome.Message);
    }

    [Fact]
    public void Validation_shows_the_servers_body_message_under_the_editor()
    {
        var problem = ProblemDetailsParser.Parse("""{"errors":{"Body":["Body is too short."]}}""");

        var outcome = FeedbackOutcomes.From(ApiResult.Failure(ApiResultKind.Validation, 400, problem));

        Assert.Equal("Body is too short.", outcome.Message);
        Assert.True(outcome.IsFieldError);
        Assert.True(outcome.KeepDraft);
    }

    [Fact]
    public void Validation_without_details_falls_back_to_a_generic_message()
    {
        var outcome = FeedbackOutcomes.From(ApiResult.Failure(ApiResultKind.Validation, 400));

        Assert.Equal(FeedbackOutcomes.ValidationFallback, outcome.Message);
    }

    [Theory]
    [InlineData(ApiResultKind.Network)]
    [InlineData(ApiResultKind.Timeout)]
    public void Network_and_timeout_share_one_message(ApiResultKind kind)
    {
        var outcome = FeedbackOutcomes.From(ApiResult.Failure(kind));

        Assert.Equal("Could not reach HexFund. Your draft is saved.", outcome.Message);
        Assert.True(outcome.KeepDraft);
    }

    [Fact]
    public void Unauthorized_asks_to_sign_in_again()
    {
        var outcome = FeedbackOutcomes.From(ApiResult.Failure(ApiResultKind.Unauthorized, 401));

        Assert.Equal("Your session has expired. Please sign out and sign in again.", outcome.Message);
        Assert.True(outcome.KeepDraft);
    }

    [Theory]
    [InlineData(ApiResultKind.Server)]
    [InlineData(ApiResultKind.Unexpected)]
    public void Server_and_unexpected_include_the_last_8_characters_of_the_correlation_id(ApiResultKind kind)
    {
        var outcome = FeedbackOutcomes.From(
            ApiResult.Failure(kind, 500, correlationId: "b1c7aaaa-1111-2222-3333-444455556666"));

        Assert.Equal(
            "Something went wrong on our side. Your draft is saved. Reference: 55556666", outcome.Message);
        Assert.True(outcome.KeepDraft);
    }

    [Fact]
    public void Server_error_without_a_correlation_id_has_no_reference()
    {
        var outcome = FeedbackOutcomes.From(ApiResult.Failure(ApiResultKind.Server, 500));

        Assert.Equal("Something went wrong on our side. Your draft is saved.", outcome.Message);
    }

    [Fact]
    public void A_short_correlation_id_is_used_whole() =>
        Assert.EndsWith("Reference: abc", FeedbackOutcomes.ServerMessageWithReference("abc"));

    [Theory]
    [InlineData(400, ApiResultKind.Validation)]
    [InlineData(401, ApiResultKind.Unauthorized)]
    [InlineData(404, ApiResultKind.Unexpected)]
    [InlineData(413, ApiResultKind.Unexpected)]
    [InlineData(429, ApiResultKind.RateLimited)]
    [InlineData(500, ApiResultKind.Server)]
    [InlineData(503, ApiResultKind.Server)]
    public void Status_codes_map_to_kinds(int status, ApiResultKind expected) =>
        Assert.Equal(expected, ApiResult.KindForStatus(status));

    [Fact]
    public void Correlation_id_falls_back_to_the_problem_body()
    {
        var problem = ProblemDetailsParser.Parse("""{"correlationId":"from-body-1"}""");

        var result = ApiResult.Failure(ApiResultKind.Server, 500, problem);

        Assert.Equal("from-body-1", result.CorrelationId);
    }

    [Fact]
    public void Typed_failure_keeps_the_details_of_the_untyped_one()
    {
        var failed = ApiResult.Failure(ApiResultKind.RateLimited, 429, retryAfter: TimeSpan.FromMinutes(3), correlationId: "id-1234");

        var typed = ApiResult<FeedbackReceipt>.FromFailure(failed);

        Assert.False(typed.IsSuccess);
        Assert.Null(typed.Value);
        Assert.Equal(ApiResultKind.RateLimited, typed.Kind);
        Assert.Equal(TimeSpan.FromMinutes(3), typed.RetryAfter);
        Assert.Equal("id-1234", typed.CorrelationId);
    }
}

public class FeedbackDiagnosticsTests
{
    private static FeedbackDiagnostics Sample() => new()
    {
        AppVersion = "1.9", BuildNumber = "23", Platform = "Android", OsVersion = "16",
        DeviceModel = "Google Pixel 8", Screen = "calendar", Theme = "Gold",
    };

    [Fact]
    public void Clip_trims_and_cuts_to_the_limit()
    {
        Assert.Equal("abc", FeedbackDiagnosticsFormat.Clip("  abc  ", 10));
        Assert.Equal("abcde", FeedbackDiagnosticsFormat.Clip("abcdefgh", 5));
        Assert.Equal(string.Empty, FeedbackDiagnosticsFormat.Clip(null, 5));
    }

    [Fact]
    public void Clipped_applies_every_field_limit_and_keeps_the_newest_20_errors()
    {
        var d = new FeedbackDiagnostics
        {
            AppVersion = new string('a', 50), BuildNumber = new string('b', 50),
            Platform = new string('c', 50), OsVersion = new string('d', 50),
            DeviceModel = new string('e', 200), Screen = new string('f', 100),
            Theme = new string('g', 50),
        };
        for (var i = 0; i < 25; i++)
            d.RecentErrors.Add(new FeedbackErrorEntry
            {
                At = DateTimeOffset.UnixEpoch.AddSeconds(i), Method = "GET", Path = new string('p', 500) + i,
                Status = 500, Error = new string('x', 300), CorrelationId = new string('z', 100),
            });

        var clipped = d.Clipped();

        Assert.Equal(20, clipped.AppVersion.Length);
        Assert.Equal(20, clipped.BuildNumber.Length);
        Assert.Equal(20, clipped.Platform.Length);
        Assert.Equal(30, clipped.OsVersion.Length);
        Assert.Equal(80, clipped.DeviceModel.Length);
        Assert.Equal(50, clipped.Screen.Length);
        Assert.Equal(30, clipped.Theme.Length);
        Assert.Equal(20, clipped.RecentErrors.Count);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(5), clipped.RecentErrors[0].At);
        Assert.All(clipped.RecentErrors, e =>
        {
            Assert.Equal(200, e.Path.Length);
            Assert.Equal(100, e.Error!.Length);
            Assert.Equal(64, e.CorrelationId!.Length);
        });
    }

    [Fact]
    public void Display_text_lists_every_value_that_is_sent()
    {
        var d = Sample();
        d.RecentErrors.Add(new FeedbackErrorEntry
        {
            At = new DateTimeOffset(2026, 10, 20, 14, 1, 22, TimeSpan.Zero), Method = "GET",
            Path = "accounts/{id}/monthly-overview", Status = 500, CorrelationId = "b1c7",
        });

        var text = d.ToDisplayText();

        Assert.Contains("App version: 1.9 (build 23)", text);
        Assert.Contains("Device: Google Pixel 8", text);
        Assert.Contains("System: Android 16", text);
        Assert.Contains("Screen: calendar", text);
        Assert.Contains("Theme: Gold", text);
        Assert.Contains("Recent request errors (1):", text);
        Assert.Contains("2026-10-20 14:01:22Z  GET accounts/{id}/monthly-overview  500  ref b1c7", text);
    }

    [Fact]
    public void Display_text_says_none_when_there_are_no_errors() =>
        Assert.EndsWith("Recent request errors: none", Sample().ToDisplayText());

    [Fact]
    public void Display_text_shows_the_exception_name_when_there_is_no_status()
    {
        var d = Sample();
        d.RecentErrors.Add(new FeedbackErrorEntry
        {
            At = DateTimeOffset.UnixEpoch, Method = "POST", Path = "feedback", Error = "Timeout",
        });

        Assert.Contains("POST feedback  Timeout", d.ToDisplayText());
    }

    [Theory]
    [InlineData("HomePage", "home")]
    [InlineData("CalendarPage", "calendar")]
    [InlineData("TransactionsPage", "ledger")]
    [InlineData("InsightsPage", "insights")]
    [InlineData("SettingsPage", "settings")]
    [InlineData("AccountsPage", "accounts")]
    [InlineData("AddEntryPage", "add")]
    [InlineData("FeedbackPage", "feedback")]
    [InlineData("LoginPage", "login")]
    [InlineData("SomethingElse", "unknown")]
    [InlineData(null, "unknown")]
    public void Page_type_names_map_to_short_screen_names(string? typeName, string expected) =>
        Assert.Equal(expected, ScreenNames.FromPageTypeName(typeName));
}

public class FeedbackRequestJsonTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void Enum_values_match_the_api()
    {
        Assert.Equal(0, (int)FeedbackType.Bug);
        Assert.Equal(1, (int)FeedbackType.Idea);
        Assert.Equal(2, (int)FeedbackType.Question);
    }

    [Fact]
    public void Request_without_diagnostics_omits_the_property()
    {
        var json = JsonSerializer.Serialize(
            new SubmitFeedbackRequest { Type = FeedbackType.Idea, Body = "a long enough message" }, Options);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("type").GetInt32());
        Assert.Equal("a long enough message", doc.RootElement.GetProperty("body").GetString());
        Assert.False(doc.RootElement.TryGetProperty("diagnostics", out _));
    }

    [Fact]
    public void Request_with_diagnostics_uses_camel_case_names()
    {
        var request = new SubmitFeedbackRequest
        {
            Type = FeedbackType.Bug, Body = "a long enough message",
            Diagnostics = new FeedbackDiagnostics { AppVersion = "1.9", DeviceModel = "Pixel" },
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(request, Options));
        var diagnostics = doc.RootElement.GetProperty("diagnostics");

        Assert.Equal("1.9", diagnostics.GetProperty("appVersion").GetString());
        Assert.Equal("Pixel", diagnostics.GetProperty("deviceModel").GetString());
        Assert.Equal(JsonValueKind.Array, diagnostics.GetProperty("recentErrors").ValueKind);
    }
}
