using Grove.Access;
using Grove.Data.Connectors.Models;
using Grove.Data.Connectors.Services;
using Grove.Documents;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Grove.Data.Connectors.Tests;

// The execution pipeline: retries and backoff, Retry-After (429), "retry after T" instead of a
// long sleep, the unsafe-operation rule, the attempts reported, the staff-only caller.
public class PipelineTests
{
    private static readonly CallerContext Staff = new() { Tenant = "t", Side = CallerSide.Admin, UserId = "u1", UserName = "staff" };

    private static ConnectionOperation Get => new() { Id = "list", Method = "GET", Path = "items", IsSafeToRetry = true };

    private static ConnectionOperation Post => new() { Id = "create", Method = "POST", Path = "items", IsSafeToRetry = false };

    private static ConnectionOperation KeyedPost => new() { Id = "create-keyed", Method = "POST", Path = "items", SupportsIdempotencyKey = true };

    [Fact]
    public async Task A_safe_operation_is_retried_on_a_transient_failure_until_it_succeeds()
    {
        var (engine, protocol, waits) = Build(Transient(), Transient(), Ok());
        var result = await engine.ExecuteAsync(Call(Get.Id));

        Assert.Equal(ConnectorOutcome.Succeeded, result.Outcome);
        Assert.Equal(3, protocol.Calls);
        Assert.Equal(3, result.Attempts.Count);
        Assert.Equal(FailureKind.Transient, result.Attempts[0].Failure);
        Assert.Equal(2, waits.Count);
    }

    [Fact]
    public async Task Retries_stop_at_the_connections_retry_count()
    {
        var (engine, protocol, _) = Build(Transient(), Transient(), Transient(), Transient());
        var result = await engine.ExecuteAsync(Call(Get.Id));

        Assert.Equal(ConnectorOutcome.Failed, result.Outcome);
        Assert.Equal(3, protocol.Calls); // RetryCount 2: one attempt and two retries.
        Assert.Equal(3, result.Attempts.Count);
    }

    [Fact]
    public async Task A_caller_can_lower_the_retries()
    {
        var (engine, protocol, _) = Build(Transient(), Ok());
        var result = await engine.ExecuteAsync(Call(Get.Id) with { MaxRetries = 0 });

        Assert.Equal(ConnectorOutcome.Failed, result.Outcome);
        Assert.Equal(1, protocol.Calls);
    }

    [Fact]
    public async Task A_short_retry_after_is_honoured_inline()
    {
        var (engine, _, waits) = Build(RateLimited(TimeSpan.FromSeconds(2)), Ok());
        var result = await engine.ExecuteAsync(Call(Get.Id));

        Assert.Equal(ConnectorOutcome.Succeeded, result.Outcome);
        Assert.Equal([TimeSpan.FromSeconds(2)], waits);
        Assert.Equal(TimeSpan.FromSeconds(2), result.Attempts[1].WaitBefore);
    }

    [Fact]
    public async Task A_long_retry_after_is_answered_not_slept()
    {
        var (engine, protocol, waits) = Build(RateLimited(TimeSpan.FromMinutes(10)), Ok());
        var before = DateTimeOffset.UtcNow;
        var result = await engine.ExecuteAsync(Call(Get.Id));

        Assert.Equal(ConnectorOutcome.RetryAfter, result.Outcome);
        Assert.Empty(waits);
        Assert.Equal(1, protocol.Calls);
        Assert.NotNull(result.RetryAt);
        Assert.True(result.RetryAt >= before + TimeSpan.FromMinutes(10));
        Assert.Single(result.Attempts);
    }

    [Fact]
    public async Task A_caller_that_never_waits_inline_gets_retry_after_for_any_429()
    {
        var (engine, _, waits) = Build(RateLimited(TimeSpan.FromSeconds(1)), Ok());
        var result = await engine.ExecuteAsync(Call(Get.Id) with { MaxInlineWait = TimeSpan.Zero });

        Assert.Equal(ConnectorOutcome.RetryAfter, result.Outcome);
        Assert.Empty(waits);
    }

    [Fact]
    public async Task An_unsafe_operation_is_not_retried_after_a_failure_that_may_have_reached_the_service()
    {
        var (engine, protocol, _) = Build(Transient(), Ok());
        var result = await engine.ExecuteAsync(Call(Post.Id));

        Assert.Equal(ConnectorOutcome.Failed, result.Outcome);
        Assert.Equal(1, protocol.Calls);
    }

    [Fact]
    public async Task An_unsafe_operation_is_retried_when_the_failure_proves_nothing_was_sent()
    {
        var (engine, protocol, _) = Build(new ProtocolResponse { Failure = new FailureClassification(FailureKind.Transient, NotSent: true), Error = "connection refused" }, Ok());
        var result = await engine.ExecuteAsync(Call(Post.Id));

        Assert.Equal(ConnectorOutcome.Succeeded, result.Outcome);
        Assert.Equal(2, protocol.Calls);
    }

    [Fact]
    public async Task An_unsafe_operation_with_an_idempotency_key_is_retried()
    {
        var (engine, protocol, _) = Build(Transient(), Ok());
        var result = await engine.ExecuteAsync(Call(KeyedPost.Id) with { IdempotencyKey = "job-1" });

        Assert.Equal(ConnectorOutcome.Succeeded, result.Outcome);
        Assert.Equal(2, protocol.Calls);
        Assert.Equal("job-1", protocol.LastIdempotencyKey);
    }

    [Fact]
    public async Task An_idempotency_key_does_not_make_an_operation_safe_when_the_service_ignores_it()
    {
        var (engine, protocol, _) = Build(Transient(), Ok());
        var result = await engine.ExecuteAsync(Call(Post.Id) with { IdempotencyKey = "job-1" });

        Assert.Equal(ConnectorOutcome.Failed, result.Outcome);
        Assert.Equal(1, protocol.Calls);
    }

    [Fact]
    public async Task A_permanent_failure_is_not_retried()
    {
        var (engine, protocol, _) = Build(new ProtocolResponse { StatusCode = 400, Failure = new FailureClassification(FailureKind.Permanent), Error = "HTTP 400" }, Ok());
        var result = await engine.ExecuteAsync(Call(Get.Id));

        Assert.Equal(ConnectorOutcome.Failed, result.Outcome);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(1, protocol.Calls);
    }

    [Fact]
    public async Task A_refusal_ends_the_call_at_once()
    {
        var (engine, protocol, _) = Build(new ConnectorRefusedException("'10.0.0.1' resolves to a non-public address"));
        var result = await engine.ExecuteAsync(Call(Get.Id));

        Assert.Equal(ConnectorOutcome.Refused, result.Outcome);
        Assert.Equal(1, protocol.Calls);
        Assert.Contains("non-public", result.Error);
    }

    [Theory]
    [InlineData(CallerSide.Member)]
    [InlineData(CallerSide.Site)]
    public async Task Member_and_site_users_cannot_call(CallerSide side)
    {
        var (engine, protocol, _) = Build(Ok());
        var caller = new CallerContext { Tenant = "t", Side = side, UserId = "m1" };
        var result = await engine.ExecuteAsync(Call(Get.Id) with { Caller = caller });

        Assert.Equal(ConnectorOutcome.Refused, result.Outcome);
        Assert.Equal(0, protocol.Calls);
    }

    [Fact]
    public async Task A_safe_operation_is_cached_per_credential()
    {
        var (engine, protocol, _) = Build(policy: new ConnectionPolicy { CacheLifetime = TimeSpan.FromMinutes(1) }, responses: [Ok(), Ok()]);
        var first = await engine.ExecuteAsync(Call(Get.Id));
        var second = await engine.ExecuteAsync(Call(Get.Id));

        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Equal(1, protocol.Calls);
    }

    [Fact]
    public async Task The_rate_limit_answers_retry_after_when_its_window_is_long()
    {
        var (engine, protocol, _) = Build(policy: new ConnectionPolicy { RateLimit = new RateLimitPolicy(1, TimeSpan.FromMinutes(1)) }, responses: [Ok(), Ok()]);
        Assert.True((await engine.ExecuteAsync(Call(Get.Id))).Succeeded);
        var limited = await engine.ExecuteAsync(Call(Get.Id));

        Assert.Equal(ConnectorOutcome.RetryAfter, limited.Outcome);
        Assert.Equal(1, protocol.Calls);
    }

    [Fact]
    public void Backoff_grows_and_is_capped()
    {
        var backoff = new BackoffPolicy { Initial = TimeSpan.FromMilliseconds(100), Factor = 2, Max = TimeSpan.FromMilliseconds(300), Jitter = false };
        Assert.Equal(TimeSpan.FromMilliseconds(100), ConnectorEngine.Backoff(backoff, 1));
        Assert.Equal(TimeSpan.FromMilliseconds(200), ConnectorEngine.Backoff(backoff, 2));
        Assert.Equal(TimeSpan.FromMilliseconds(300), ConnectorEngine.Backoff(backoff, 3));
    }

    [Fact]
    public void Retry_after_headers_parse_as_seconds_or_a_date()
    {
        var now = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromSeconds(120), Protocols.Http.HttpFailures.RetryAfter("120", now));
        Assert.Equal(TimeSpan.FromSeconds(30), Protocols.Http.HttpFailures.RetryAfter("Thu, 15 Oct 2026 12:00:30 GMT", now));
        Assert.Equal(TimeSpan.Zero, Protocols.Http.HttpFailures.RetryAfter("Thu, 15 Oct 2026 11:00:00 GMT", now));
        Assert.Null(Protocols.Http.HttpFailures.RetryAfter("soon", now));
    }

    [Theory]
    [InlineData(429, FailureKind.RateLimited)]
    [InlineData(503, FailureKind.Transient)]
    [InlineData(408, FailureKind.Transient)]
    [InlineData(401, FailureKind.Unauthorized)]
    [InlineData(404, FailureKind.Permanent)]
    [InlineData(200, FailureKind.None)]
    public void Http_statuses_are_classified(int status, FailureKind kind) =>
        Assert.Equal(kind, Protocols.Http.HttpFailures.Classify(status).Kind);

    [Theory]
    [InlineData("GET", true)]
    [InlineData("FETCH", true)]
    [InlineData("SEND", false)]
    [InlineData("DELETE", false)]
    [InlineData("POST", false)]
    public void Raw_requests_are_safe_only_when_they_read(string method, bool safe) =>
        Assert.Equal(safe, ConnectorEngine.IsSafeRequest(new ConnectionDefinition { Key = "mail", DisplayName = "Mail", Protocol = "imap" }, new ConnectorRequest(method, null)));

    [Fact]
    public void A_protocols_policy_defaults_apply_where_the_connection_kept_the_stock_values()
    {
        IConnectorProtocol protocol = new MailLikeProtocol();
        var stock = new ConnectionDefinition { Key = "smtp", DisplayName = "SMTP", Protocol = "smtp" };
        var own = new ConnectionDefinition { Key = "smtp", DisplayName = "SMTP", Protocol = "smtp", Policy = new ConnectionPolicy { MaxRequestBytes = 2 * 1024 * 1024 } };

        Assert.Equal(25 * 1024 * 1024, ConnectorEngine.Effective(stock, protocol).Policy.MaxRequestBytes);
        Assert.Equal(2 * 1024 * 1024, ConnectorEngine.Effective(own, protocol).Policy.MaxRequestBytes);
        Assert.Equal(50 * 1024 * 1024, ConnectorEngine.Effective(own, protocol).Policy.MaxResponseBytes);
    }

    [Fact]
    public async Task Client_secret_basic_sends_the_client_in_an_authorization_header()
    {
        var handler = new RecordingHandler("{\"access_token\":\"t\",\"expires_in\":60}");
        var connection = new ConnectionBuilder("x").OAuth2ClientCredentials("https://auth.example.com/token").Build();
        connection = new ConnectionDefinition { Key = connection.Key, DisplayName = connection.DisplayName, Protocol = connection.Protocol, Auth = connection.Auth with { Settings = new Dictionary<string, string> { ["tokenEndpointAuth"] = "client_secret_basic" } } };
        var result = await new Protocols.OAuth.OAuthClient().RequestClientCredentialsAsync(new HttpClient(handler), connection, new AppCredentials("id", "secret"));

        Assert.True(result.Succeeded);
        Assert.Equal("Basic", handler.Authorization?.Scheme);
        Assert.Equal(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("id:secret")), handler.Authorization?.Parameter);
        Assert.DoesNotContain("client_secret", handler.Body);
    }

    private sealed class MailLikeProtocol : IConnectorProtocol
    {
        public string Id => "smtp";

        public string DisplayName => "Mail";

        public IReadOnlyList<string> AuthSchemes => [Connectors.AuthSchemes.Basic];

        public IReadOnlyList<string> SchemaFormats => [];

        public ConnectionPolicy? DefaultPolicy => new() { MaxRequestBytes = 25 * 1024 * 1024, MaxResponseBytes = 50 * 1024 * 1024 };

        public Task<SchemaReadResult> ReadOperationsAsync(ConnectionDefinition connection, ConnectionSchemaDocument schema, CancellationToken cancellationToken = default) => Task.FromResult(new SchemaReadResult([]));

        public Task<ProtocolResponse> SendAsync(IProtocolCallContext context, CancellationToken cancellationToken = default) => Task.FromResult(new ProtocolResponse());

        public FailureClassification Classify(Exception exception) => new(FailureKind.Permanent);
    }

    private sealed class RecordingHandler(string response) : HttpMessageHandler
    {
        public System.Net.Http.Headers.AuthenticationHeaderValue? Authorization { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json") };
        }
    }

    private static ConnectorCall Call(string operationId) => new() { ConnectionKey = "api", Caller = Staff, OperationId = operationId };

    private static ProtocolResponse Ok() => new() { StatusCode = 200, Body = "{}" };

    private static ProtocolResponse Transient() => new() { StatusCode = 503, Failure = new FailureClassification(FailureKind.Transient), Error = "HTTP 503" };

    private static ProtocolResponse RateLimited(TimeSpan wait) => new() { StatusCode = 429, Failure = new FailureClassification(FailureKind.RateLimited, wait), Error = "HTTP 429" };

    private static (ConnectorEngine Engine, FakeProtocol Protocol, List<TimeSpan> Waits) Build(params object[] responses) => Build(null, responses);

    private static (ConnectorEngine Engine, FakeProtocol Protocol, List<TimeSpan> Waits) Build(ConnectionPolicy? policy, object[] responses)
    {
        var connection = new ConnectionDefinition
        {
            Key = "api",
            DisplayName = "API",
            Protocol = "fake",
            BaseUrl = "https://api.example.com/",
            Operations = [Get, Post, KeyedPost],
            Policy = policy ?? new ConnectionPolicy { Backoff = new BackoffPolicy { Jitter = false } },
            Version = "v1",
        };
        var registry = new FakeRegistry(connection);
        var protocol = new FakeProtocol(responses);
        var store = Substitute.For<ICredentialStore>();
        var resolver = new CredentialResolver(store, [new TenantScopeHandler(), new UserScopeHandler()]);
        var http = new ConnectorHttp(Options.Create(new ConnectorOptions()));
        var credentialStore = new CredentialStore(Substitute.For<IDocumentManager<ConnectorCredentialsDocument>>(), new EphemeralDataProtectionProvider());
        var tokens = new ConnectorTokenService(Substitute.For<IOAuthClient>(), http, store, new AppCredentialsProvider(credentialStore, registry), new ConnectorTokenCache());
        var engine = new ConnectorEngine(registry, [protocol], resolver, tokens, http, new ConnectorLimits(), new MemoryCache(new MemoryCacheOptions()), Substitute.For<IServiceProvider>(), NullLogger<ConnectorEngine>.Instance);
        var waits = new List<TimeSpan>();
        engine.Delay = (wait, _) =>
        {
            waits.Add(wait);
            return Task.CompletedTask;
        };

        return (engine, protocol, waits);
    }

    private sealed class FakeRegistry(ConnectionDefinition connection) : IConnectionRegistry
    {
        public Task<IReadOnlyList<ConnectionDefinition>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ConnectionDefinition>>([connection]);

        public Task<ConnectionDefinition?> FindAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(key == connection.Key ? connection : null);

        public Task<IReadOnlyList<ConnectionOperation>> GetOperationsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(connection.Operations);

        public Task<ConnectionOperation?> FindOperationAsync(string key, string operationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(connection.Operations.FirstOrDefault(o => o.Id == operationId));

        public Task<IReadOnlyList<ConnectionOperationEntry>> ListOperationsAsync(string? protocol = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ConnectionOperationEntry>>(connection.Operations.Select(o => new ConnectionOperationEntry(connection.Key, connection.DisplayName, o)).ToArray());
    }

    private sealed class FakeProtocol(object[] responses) : IConnectorProtocol
    {
        public int Calls { get; private set; }

        public string? LastIdempotencyKey { get; private set; }

        public string Id => "fake";

        public string DisplayName => "Fake";

        public IReadOnlyList<string> AuthSchemes => [Connectors.AuthSchemes.None];

        public IReadOnlyList<string> SchemaFormats => [];

        public Task<SchemaReadResult> ReadOperationsAsync(ConnectionDefinition connection, ConnectionSchemaDocument schema, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SchemaReadResult([]));

        public Task<ProtocolResponse> SendAsync(IProtocolCallContext context, CancellationToken cancellationToken = default)
        {
            LastIdempotencyKey = context.IdempotencyKey;
            var next = responses[Math.Min(Calls, responses.Length - 1)];
            Calls++;
            return next is Exception ex ? Task.FromException<ProtocolResponse>(ex) : Task.FromResult((ProtocolResponse)next);
        }

        public FailureClassification Classify(Exception exception) => new(FailureKind.Transient);
    }
}
