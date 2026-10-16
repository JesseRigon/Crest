using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Data.Connectors;

namespace Grove.Connectors.Consumers.Tests;

/// <summary>An engine that records every call and answers what the test set.</summary>
public sealed class FakeConnectorEngine : IConnectorEngine
{
    public List<ConnectorCall> Calls { get; } = [];

    public Func<ConnectorCall, ConnectorResult> Answer { get; set; } = _ => new ConnectorResult { Outcome = ConnectorOutcome.Succeeded, StatusCode = 200 };

    /// <summary>The connections <see cref="RunSdkAsync{T}"/> opens, with their tenant credential.</summary>
    public Dictionary<string, (ConnectionDefinition Connection, ConnectionCredential? Credential)> SdkConnections { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<(string Key, CallerContext Caller, SdkCallOptions? Options)> SdkCalls { get; } = [];

    public Task<ConnectorResult> ExecuteAsync(ConnectorCall call, CancellationToken cancellationToken = default)
    {
        Calls.Add(call);
        return Task.FromResult(Answer(call));
    }

    /// <summary>The guarded handler the SDK context's HTTP client sends on.</summary>
    public HttpMessageHandler? SdkHttp { get; set; }

    /// <summary>
    /// Runs the SDK call like the engine's policy would: retries a retryable failure (by the
    /// call's classifier) up to the call's or the connection's retry count, never an unsafe one
    /// unless nothing was sent; a refusal is never retried.
    /// </summary>
    public async Task<ConnectorResult<T>> RunSdkAsync<T>(string connectionKey, CallerContext caller, Func<ISdkCallContext, CancellationToken, Task<T>> call, SdkCallOptions? options = null, CancellationToken cancellationToken = default)
    {
        SdkCalls.Add((connectionKey, caller, options));
        if (!SdkConnections.TryGetValue(connectionKey, out var entry))
        {
            return new ConnectorResult<T> { Outcome = ConnectorOutcome.Refused, Error = $"No connection '{connectionKey}'." };
        }

        var retries = options?.MaxRetries ?? entry.Connection.Policy.RetryCount;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var result = await call(new FakeSdkContext(entry.Connection, entry.Credential, attempt, SdkHttp), cancellationToken);
                return new ConnectorResult<T> { Outcome = ConnectorOutcome.Succeeded, Result = result };
            }
            catch (Exception exception)
            {
                var failure = options?.Classify?.Invoke(exception) ?? new FailureClassification(FailureKind.Permanent);
                if (failure.Kind == FailureKind.Refused)
                {
                    return new ConnectorResult<T> { Outcome = ConnectorOutcome.Refused, Error = exception.Message, Failure = failure.Kind };
                }

                if (failure.IsRetryable && attempt <= retries && (options?.IsSafeToRetry == true || failure.NotSent))
                {
                    continue;
                }

                return new ConnectorResult<T> { Outcome = ConnectorOutcome.Failed, Error = exception.Message, Failure = failure.Kind };
            }
        }
    }

    public static ConnectorResult Rows(JsonNode value, string? nextPage = null) =>
        new() { Outcome = ConnectorOutcome.Succeeded, StatusCode = 200, Value = value, Body = value.ToJsonString(), NextPage = nextPage };

    private sealed class FakeSdkContext(ConnectionDefinition connection, ConnectionCredential? credential, int attempt, HttpMessageHandler? http) : ISdkCallContext
    {
        public ConnectionDefinition Connection { get; } = connection;

        public ConnectionCredential? Credential { get; } = credential;

        public CredentialOwner? Owner => CredentialOwner.Tenant;

        public int Attempt { get; } = attempt;

        public HttpClient HttpClient => new(Handler, disposeHandler: false);

        public HttpMessageHandler Handler => http ?? throw new NotSupportedException();

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public IServiceProvider Services => throw new NotSupportedException();
    }
}

public sealed class FakeCallerAccessor : ICallerContextAccessor
{
    public CallerContext? Current { get; set; }
}

public sealed class FakeCallerFactory : ICallerContextFactory
{
    public static readonly CallerContext System = new() { Tenant = "Default", Side = CallerSide.System, IsSystem = true, IsSuperUser = true };

    public int SystemCallers { get; private set; }

    public Task<CallerContext> CreateAsync(CallerRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<CallerContext> CreateSystemAsync(string? organizationId = null, CancellationToken cancellationToken = default)
    {
        SystemCallers++;
        return Task.FromResult(System);
    }
}

public sealed class FakeAppCredentials(Dictionary<string, AppCredentials> stored) : IAppCredentialsProvider
{
    public List<string> Reads { get; } = [];

    public Task<AppCredentials?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        Reads.Add(key);
        return Task.FromResult(stored.TryGetValue(key, out var credentials) ? credentials : null);
    }
}

public sealed class FakeCredentialStore : ICredentialStore
{
    public Dictionary<(string Key, CredentialOwner Owner), ConnectionCredential> Saved { get; } = [];

    public Task<IReadOnlyList<CredentialInfo>> ListAsync(string? connectionKey = null, CredentialOwner? owner = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<CredentialInfo?> FindAsync(string connectionKey, CredentialOwner owner, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ConnectionCredential?> RevealAsync(string connectionKey, CredentialOwner owner, CancellationToken cancellationToken = default) =>
        Task.FromResult(Saved.TryGetValue((connectionKey, owner), out var credential) ? credential : null);

    public Task<CredentialInfo> SaveAsync(string connectionKey, CredentialOwner owner, ConnectionCredential credential, string? label = null, bool merge = true, CancellationToken cancellationToken = default)
    {
        Saved[(connectionKey, owner)] = credential;
        return Task.FromResult(new CredentialInfo { ConnectionKey = connectionKey, Owner = owner, Label = label, ValueNames = [.. credential.Values.Keys] });
    }

    public Task StoreTokensAsync(string connectionKey, CredentialOwner owner, OAuthTokenSet tokens, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<bool> DeleteAsync(string connectionKey, CredentialOwner owner, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

public static class Callers
{
    public static readonly CallerContext Staff = new() { Tenant = "Default", Side = CallerSide.Admin, UserId = "staff-1", UserName = "staff" };

    public static readonly CallerContext Member = new() { Tenant = "Default", Side = CallerSide.Member, UserId = "member-1", UserName = "member" };
}

/// <summary>Answers with the next queued status (and body), recording each request.</summary>
public sealed class StubHttpHandler(params (int Status, string Body)[] answers) : HttpMessageHandler
{
    private int _next;

    public List<(HttpMethod Method, Uri? Uri, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.Method, request.RequestUri, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
        var (status, body) = answers[Math.Min(_next++, answers.Length - 1)];

        return new HttpResponseMessage((System.Net.HttpStatusCode)status) { Content = new StringContent(body) };
    }
}
