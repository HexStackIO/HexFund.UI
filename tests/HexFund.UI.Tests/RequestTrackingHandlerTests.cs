using System.Net;
using HexFund.UI.Services.Http;

namespace HexFund.UI.Tests;

public class RequestTrackingHandlerTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Respond(request, ct);
        }
    }

    private static (HttpClient Client, FailureBuffer Buffer, StubHandler Stub) Create(string version = "1.9")
    {
        var buffer = new FailureBuffer();
        var stub = new StubHandler();
        var handler = new RequestTrackingHandler(buffer, version) { InnerHandler = stub };
        return (new HttpClient(handler) { BaseAddress = new Uri("https://host.test/api/") }, buffer, stub);
    }

    [Fact]
    public async Task Adds_a_fresh_valid_correlation_id_and_the_app_version()
    {
        var (client, _, stub) = Create("1.9");

        await client.GetAsync("accounts");
        var first = stub.LastRequest!.Headers.GetValues(RequestTrackingHandler.CorrelationHeader).Single();
        await client.GetAsync("accounts");
        var second = stub.LastRequest!.Headers.GetValues(RequestTrackingHandler.CorrelationHeader).Single();

        Assert.Matches("^[A-Za-z0-9-]{8,64}$", first);
        Assert.NotEqual(first, second);
        Assert.Equal("1.9", stub.LastRequest.Headers.GetValues(RequestTrackingHandler.VersionHeader).Single());
    }

    [Fact]
    public async Task Cuts_the_app_version_to_20_characters()
    {
        var (client, _, stub) = Create(new string('9', 40));

        await client.GetAsync("accounts");

        Assert.Equal(20, stub.LastRequest!.Headers.GetValues(RequestTrackingHandler.VersionHeader).Single().Length);
    }

    [Fact]
    public async Task Successful_calls_are_not_recorded()
    {
        var (client, buffer, _) = Create();

        await client.GetAsync("accounts");

        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public async Task Failed_responses_are_recorded_with_a_sanitized_path_and_no_query()
    {
        var (client, buffer, stub) = Create();
        stub.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await client.GetAsync("accounts/3fa85f64-5717-4562-b3fc-2c963f66afa6/transactions?secret=1");

        var record = Assert.Single(buffer.Snapshot());
        Assert.Equal("GET", record.Method);
        Assert.Equal("accounts/{id}/transactions", record.Path);
        Assert.Equal(500, record.Status);
        Assert.Null(record.Error);
    }

    [Fact]
    public async Task Uses_the_servers_correlation_id_when_it_sends_one()
    {
        var (client, buffer, stub) = Create();
        stub.Respond = (_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
            response.Headers.Add(RequestTrackingHandler.CorrelationHeader, "server-id-1234");
            return Task.FromResult(response);
        };

        await client.GetAsync("accounts");

        Assert.Equal("server-id-1234", buffer.Snapshot().Single().CorrelationId);
    }

    [Fact]
    public async Task Thrown_exceptions_are_recorded_by_type_name_only()
    {
        var (client, buffer, stub) = Create();
        stub.Respond = (_, _) => throw new HttpRequestException("secret detail in the message");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("accounts"));

        var record = Assert.Single(buffer.Snapshot());
        Assert.Equal("HttpRequestException", record.Error);
        Assert.Null(record.Status);
        Assert.DoesNotContain("secret", record.Error);
    }

    [Fact]
    public async Task A_timeout_is_recorded_as_Timeout()
    {
        var (client, buffer, stub) = Create();
        stub.Respond = (_, _) => throw new TaskCanceledException("timed out");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("accounts"));

        Assert.Equal("Timeout", buffer.Snapshot().Single().Error);
    }

    [Fact]
    public async Task A_cancellation_requested_by_the_caller_is_not_recorded()
    {
        var (client, buffer, stub) = Create();
        using var cts = new CancellationTokenSource();
        stub.Respond = (_, ct) =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage());
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("accounts", cts.Token));

        Assert.Empty(buffer.Snapshot());
    }
}
