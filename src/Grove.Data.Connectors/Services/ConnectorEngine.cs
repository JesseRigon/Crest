using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Data.Protocols.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Grove.Data.Connectors.Services;

/// <summary>
/// The connector engine's pipeline (docs/connectors.md › Execution). Per call: the connection
/// and operation from the registry, the caller's credential from the resolver, the cache for
/// a safe operation (per credential owner), then attempts: rate limit and quota (a long wait
/// becomes <see cref="ConnectorOutcome.RetryAfter"/>), the protocol's attempt under the
/// connection's timeout, the protocol's classification, and a retry with backoff (or the
/// service's Retry-After) when the failure is retryable and the operation is safe, carries an
/// idempotency key, or provably never left. A refused OAuth token is refreshed once.
/// </summary>
public sealed class ConnectorEngine(
    IConnectionRegistry registry,
    IEnumerable<IConnectorProtocol> protocols,
    ICredentialResolver resolver,
    ConnectorTokenService tokens,
    ConnectorHttp http,
    ConnectorLimits limits,
    IMemoryCache cache,
    IServiceProvider services,
    ILogger<ConnectorEngine> logger) : IConnectorEngine
{
    private ConnectorHttp Http { get; } = http;

    private IServiceProvider Services { get; } = services;

    private ConnectorTokenService Tokens { get; } = tokens;

    /// <summary>For tests: the clock and the sleep.</summary>
    internal Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.UtcNow;

    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

    public async Task<ConnectorResult> ExecuteAsync(ConnectorCall call, CancellationToken cancellationToken = default)
    {
        if (!ConnectorCallers.MayCall(call.Caller))
        {
            return Refused("Connections are used from the admin side only.");
        }

        var connection = await registry.FindAsync(call.ConnectionKey, cancellationToken);
        if (connection is null)
        {
            return Refused($"No connection '{call.ConnectionKey}' in this tenant.");
        }

        var protocol = protocols.FirstOrDefault(p => string.Equals(p.Id, connection.Protocol, StringComparison.OrdinalIgnoreCase));
        if (protocol is null)
        {
            return Refused($"No protocol '{connection.Protocol}' is registered for connection '{connection.Key}'.");
        }

        connection = Effective(connection, protocol);
        ConnectionOperation? operation = null;
        if (call.OperationId is { Length: > 0 } operationId)
        {
            operation = await registry.FindOperationAsync(connection.Key, operationId, cancellationToken);
            if (operation is null)
            {
                return Refused($"Connection '{connection.Key}' has no operation '{operationId}'.");
            }
        }
        else if (call.Request is null)
        {
            return Refused("A call names an operation or carries a request.");
        }

        if (call.Request?.Body is { } body && Encoding.UTF8.GetByteCount(body) > connection.Policy.MaxRequestBytes)
        {
            return Refused($"The request is larger than {connection.Policy.MaxRequestBytes} bytes.");
        }

        var credential = await resolver.ResolveAsync(connection, call.Caller, call.Scope, cancellationToken);
        if (!credential.Succeeded)
        {
            return new ConnectorResult { Outcome = credential.Failure == CredentialResolutionFailure.NotStaff ? ConnectorOutcome.Refused : ConnectorOutcome.NotAuthorized, Error = credential.Error, Failure = FailureKind.Unauthorized };
        }

        var safe = operation?.IsSafeToRetry ?? IsSafeRequest(connection, call.Request!);
        var keyed = call.IdempotencyKey is { Length: > 0 } && (operation is null || operation.SupportsIdempotencyKey);
        var cacheKey = safe && connection.Policy.CacheLifetime is { } lifetime && lifetime > TimeSpan.Zero ? CacheKey(connection, credential.Owner, call) : null;
        if (cacheKey is not null && !call.BypassCache && cache.TryGetValue(cacheKey, out ConnectorResult? hit) && hit is not null)
        {
            return hit with { FromCache = true, Attempts = [] };
        }

        return await RunAsync(connection, protocol, call.Caller, credential, safe || keyed, call.MaxRetries, call.MaxInlineWait, cacheKey, async (attempt, current, ct) =>
        {
            var context = new ProtocolCallContext(this, connection, operation, call, current, attempt);
            return await protocol.SendAsync(context, ct);
        }, protocol.Classify, cancellationToken);
    }

    public async Task<ConnectorResult<T>> RunSdkAsync<T>(string connectionKey, CallerContext caller, Func<ISdkCallContext, CancellationToken, Task<T>> call, SdkCallOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new SdkCallOptions();
        if (!ConnectorCallers.MayCall(caller))
        {
            return new ConnectorResult<T> { Outcome = ConnectorOutcome.Refused, Failure = FailureKind.Refused, Error = "Connections are used from the admin side only." };
        }

        var connection = await registry.FindAsync(connectionKey, cancellationToken);
        if (connection is null)
        {
            return new ConnectorResult<T> { Outcome = ConnectorOutcome.Refused, Failure = FailureKind.Refused, Error = $"No connection '{connectionKey}' in this tenant." };
        }

        var credential = await resolver.ResolveAsync(connection, caller, options.Scope, cancellationToken);
        if (!credential.Succeeded)
        {
            return new ConnectorResult<T> { Outcome = credential.Failure == CredentialResolutionFailure.NotStaff ? ConnectorOutcome.Refused : ConnectorOutcome.NotAuthorized, Failure = FailureKind.Unauthorized, Error = credential.Error };
        }

        if (protocols.FirstOrDefault(p => string.Equals(p.Id, connection.Protocol, StringComparison.OrdinalIgnoreCase)) is { } sdkProtocol)
        {
            connection = Effective(connection, sdkProtocol);
        }

        var classify = options.Classify ?? DefaultSdkClassify;
        T? value = default;
        var result = await RunAsync(connection, null, caller, credential, options.IsSafeToRetry, options.MaxRetries, options.MaxInlineWait, null, async (attempt, current, ct) =>
        {
            value = await call(new SdkContext(this, connection, current, attempt), ct);
            return new ProtocolResponse { StatusCode = 200 };
        }, classify, cancellationToken);

        return new ConnectorResult<T>
        {
            Outcome = result.Outcome,
            StatusCode = result.StatusCode,
            Error = result.Error,
            Failure = result.Failure,
            RetryAt = result.RetryAt,
            Attempts = result.Attempts,
            RanAs = result.RanAs,
            Result = result.Succeeded ? value : default,
        };
    }

    private async Task<ConnectorResult> RunAsync(
        ConnectionDefinition connection,
        IConnectorProtocol? protocol,
        CallerContext caller,
        CredentialResolution credential,
        bool retrySafe,
        int? maxRetries,
        TimeSpan? maxInlineWait,
        string? cacheKey,
        Func<int, CredentialResolution, CancellationToken, Task<ProtocolResponse>> attemptAsync,
        Func<Exception, FailureClassification> classify,
        CancellationToken cancellationToken)
    {
        var policy = connection.Policy;
        var retries = Math.Clamp(Math.Min(policy.RetryCount, maxRetries ?? int.MaxValue), 0, 10);
        var inlineWait = maxInlineWait is { } lower && lower < policy.MaxInlineWait ? lower : policy.MaxInlineWait;
        var attempts = new List<ConnectorAttempt>();
        var refreshed = false;
        TimeSpan? waitBefore = null;
        ProtocolResponse? last = null;

        for (var attempt = 1; ; attempt++)
        {
            if (!limits.TryAcquire(connection, credential.Owner, out var rateWait) || !limits.TryCount(connection, Now().UtcDateTime, out rateWait))
            {
                if (rateWait > inlineWait)
                {
                    return RetryAfter(connection, credential, attempts, rateWait, $"The rate limit or quota of connection '{connection.Key}' is spent.");
                }

                await Delay(rateWait, cancellationToken);
                attempt--;
                continue;
            }

            var started = Now();
            ProtocolResponse response;
            FailureClassification failure;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(policy.Timeout);
                try
                {
                    response = await attemptAsync(attempt, credential, timeout.Token);
                    failure = response.Failure;
                }
                catch (ConnectorNotAuthorizedException ex)
                {
                    attempts.Add(new ConnectorAttempt(attempt, started.UtcDateTime, Now() - started, 0, FailureKind.Unauthorized, ex.Message, waitBefore));
                    return new ConnectorResult { Outcome = ConnectorOutcome.NotAuthorized, Failure = FailureKind.Unauthorized, Error = ex.Message, Attempts = attempts, RanAs = credential.Owner };
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    failure = ex is OperationCanceledException ? new FailureClassification(FailureKind.Transient) : FindRefusal(ex) is not null ? new FailureClassification(FailureKind.Refused, NotSent: true) : classify(ex);
                    var message = ex is OperationCanceledException ? $"Timed out after {policy.Timeout.TotalSeconds:0}s." : FindRefusal(ex)?.Message ?? ex.Message;
                    response = ProtocolResponse.Failed(failure, message);
                }
            }

            last = response;
            attempts.Add(new ConnectorAttempt(attempt, started.UtcDateTime, Now() - started, response.StatusCode, failure.Kind, response.Error, waitBefore));
            if (!failure.IsFailure)
            {
                var success = new ConnectorResult
                {
                    Outcome = ConnectorOutcome.Succeeded,
                    StatusCode = response.StatusCode,
                    Body = response.Body,
                    Value = response.Value,
                    Headers = response.Headers,
                    Attempts = attempts,
                    NextPage = response.NextPage,
                    RanAs = credential.Owner,
                };
                if (cacheKey is not null)
                {
                    cache.Set(cacheKey, success with { Attempts = [] }, policy.CacheLifetime!.Value);
                }

                return success;
            }

            if (failure.Kind == FailureKind.Refused)
            {
                return Failed(ConnectorOutcome.Refused, response, failure, attempts, credential);
            }

            if (failure.Kind == FailureKind.Unauthorized && !refreshed && AuthSchemes.IsOAuth2(connection.Auth.Scheme))
            {
                // The service refused the token: refresh it once and try again (not a counted retry).
                refreshed = true;
                try
                {
                    await Tokens.GetAccessTokenAsync(connection, credential.Owner, credential.Credential, forceRefresh: true, cancellationToken);
                    credential = await resolver.ResolveAsync(connection, caller, credential.Owner?.Scope, cancellationToken) is { Succeeded: true } again ? again : credential;
                }
                catch (ConnectorNotAuthorizedException ex)
                {
                    return new ConnectorResult { Outcome = ConnectorOutcome.NotAuthorized, StatusCode = response.StatusCode, Body = response.Body, Failure = FailureKind.Unauthorized, Error = ex.Message, Attempts = attempts, RanAs = credential.Owner };
                }

                waitBefore = null;
                retries++;
                if (attempt <= retries)
                {
                    continue;
                }
            }

            var mayRetry = failure.IsRetryable && (retrySafe || failure.NotSent);
            if (!mayRetry)
            {
                return Failed(ConnectorOutcome.Failed, response, failure, attempts, credential);
            }

            var wait = failure.RetryAfter ?? Backoff(policy.Backoff, attempt);
            if (attempt > retries)
            {
                // Out of retries: a service that said when to come back is answered with that time.
                return failure.Kind == FailureKind.RateLimited && failure.RetryAfter is { } later
                    ? RetryAfter(connection, credential, attempts, later, response.Error)
                    : Failed(ConnectorOutcome.Failed, response, failure, attempts, credential);
            }

            if (wait > inlineWait)
            {
                return RetryAfter(connection, credential, attempts, wait, response.Error);
            }

            logger.LogDebug("Connection {Connection} attempt {Attempt} failed ({Kind}); retrying in {Wait}.", connection.Key, attempt, failure.Kind, wait);
            waitBefore = wait;
            await Delay(wait, cancellationToken);
        }
    }

    internal static TimeSpan Backoff(BackoffPolicy backoff, int attempt)
    {
        var delay = backoff.Initial.TotalMilliseconds * Math.Pow(backoff.Factor <= 1 ? 1 : backoff.Factor, attempt - 1);
        delay = Math.Min(delay, backoff.Max.TotalMilliseconds);
        if (backoff.Jitter)
        {
            delay *= 0.5 + (Random.Shared.NextDouble() / 2);
        }

        return TimeSpan.FromMilliseconds(Math.Max(0, delay));
    }

    private ConnectorResult RetryAfter(ConnectionDefinition connection, CredentialResolution credential, List<ConnectorAttempt> attempts, TimeSpan wait, string? error) => new()
    {
        Outcome = ConnectorOutcome.RetryAfter,
        Failure = FailureKind.RateLimited,
        RetryAt = Now() + wait,
        Error = error ?? $"Connection '{connection.Key}' asked to retry later.",
        StatusCode = attempts.LastOrDefault()?.StatusCode ?? 0,
        Attempts = attempts,
        RanAs = credential.Owner,
    };

    private static ConnectorResult Failed(ConnectorOutcome outcome, ProtocolResponse response, FailureClassification failure, List<ConnectorAttempt> attempts, CredentialResolution credential) => new()
    {
        Outcome = outcome,
        StatusCode = response.StatusCode,
        Body = response.Body,
        Value = response.Value,
        Headers = response.Headers,
        Error = response.Error,
        Failure = failure.Kind,
        Attempts = attempts,
        RanAs = credential.Owner,
    };

    private static ConnectorResult Refused(string error) => new() { Outcome = ConnectorOutcome.Refused, Failure = FailureKind.Refused, Error = error };

    /// <summary>The methods of a raw request that only read: HTTP's safe methods, a mail FETCH, a SQL QUERY. Anything else (SEND, DELETE, EXECUTE, POST, ...) is unsafe.</summary>
    internal static bool IsSafeRequest(ConnectionDefinition connection, ConnectorRequest request) =>
        ConnectionOperation.IsSafeMethod(request.Method)
        || string.Equals(request.Method, "FETCH", StringComparison.OrdinalIgnoreCase)
        || (connection.Protocol == ConnectorProtocols.Sql && string.Equals(request.Method, "QUERY", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The connection with its protocol's policy defaults applied wherever the connection's own
    /// policy kept <see cref="ConnectionPolicy"/>'s default.
    /// </summary>
    internal static ConnectionDefinition Effective(ConnectionDefinition connection, IConnectorProtocol protocol)
    {
        if (protocol.DefaultPolicy is not { } defaults)
        {
            return connection;
        }

        var stock = new ConnectionPolicy();
        var own = connection.Policy;
        var policy = own with
        {
            RetryCount = own.RetryCount == stock.RetryCount ? defaults.RetryCount : own.RetryCount,
            Backoff = own.Backoff == stock.Backoff ? defaults.Backoff : own.Backoff,
            Timeout = own.Timeout == stock.Timeout ? defaults.Timeout : own.Timeout,
            RateLimit = own.RateLimit ?? defaults.RateLimit,
            Quota = own.Quota ?? defaults.Quota,
            CacheLifetime = own.CacheLifetime ?? defaults.CacheLifetime,
            MaxResponseBytes = own.MaxResponseBytes == stock.MaxResponseBytes ? defaults.MaxResponseBytes : own.MaxResponseBytes,
            MaxRequestBytes = own.MaxRequestBytes == stock.MaxRequestBytes ? defaults.MaxRequestBytes : own.MaxRequestBytes,
            MaxInlineWait = own.MaxInlineWait == stock.MaxInlineWait ? defaults.MaxInlineWait : own.MaxInlineWait,
        };
        if (policy == own)
        {
            return connection;
        }

        return new ConnectionDefinition
        {
            Key = connection.Key,
            DisplayName = connection.DisplayName,
            Description = connection.Description,
            Protocol = connection.Protocol,
            Origin = connection.Origin,
            FeatureId = connection.FeatureId,
            BaseUrl = connection.BaseUrl,
            Endpoints = connection.Endpoints,
            Schema = connection.Schema,
            Auth = connection.Auth,
            AllowedScopes = connection.AllowedScopes,
            Policy = policy,
            Operations = connection.Operations,
            Settings = connection.Settings,
            CallerRoles = connection.CallerRoles,
            Version = connection.Version,
        };
    }

    private static string CacheKey(ConnectionDefinition connection, CredentialOwner? owner, ConnectorCall call)
    {
        var material = string.Join('\u001f',
            connection.Version, owner?.ToString() ?? "-", call.OperationId ?? "-", call.Request?.Method ?? "-", call.Request?.Path ?? "-",
            call.Request?.Body ?? "-", call.Arguments?.ToJsonString() ?? "-", call.Page ?? "-",
            string.Join(',', (call.Request?.Headers ?? new Dictionary<string, string>()).OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase).Select(h => $"{h.Key}={h.Value}")));
        return "connectors:response:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static ConnectorRefusedException? FindRefusal(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is ConnectorRefusedException refused)
            {
                return refused;
            }
        }

        return null;
    }

    private static FailureClassification DefaultSdkClassify(Exception exception) => HttpFailures.Classify(exception);

    /// <summary>One attempt's context for a protocol.</summary>
    private sealed class ProtocolCallContext(ConnectorEngine engine, ConnectionDefinition connection, ConnectionOperation? operation, ConnectorCall call, CredentialResolution credential, int attempt) : IProtocolCallContext
    {
        public ConnectionDefinition Connection => connection;
        public ConnectionOperation? Operation => operation;
        public JsonObject? Arguments => call.Arguments;
        public ConnectorRequest? Request => call.Request;
        public ConnectionCredential? Credential => credential.Credential;
        public string? IdempotencyKey => call.IdempotencyKey;
        public string? Page => call.Page;
        public int Attempt => attempt;
        public HttpClient HttpClient => engine.Http.For(connection).Client;
        public bool AllowPrivateNetworks => engine.Http.Options.AllowPrivateNetworks;
        public long MaxResponseBytes => Math.Min(connection.Policy.MaxResponseBytes, engine.Http.Options.MaxResponseBytes);
        public IServiceProvider Services => engine.Services;

        public Uri ResolveUri(string? relativePath) =>
            BaseUrlPath.TryResolve(connection.BaseUrl, relativePath, out var uri, out var error) ? uri : throw new ConnectorRefusedException(error);

        public Task AuthorizeAsync(HttpRequestMessage message, CancellationToken cancellationToken = default) =>
            engine.AuthorizeAsync(connection, credential, message, cancellationToken);

        public Task<string?> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken = default) =>
            ConnectorHttp.ReadCappedAsync(response, MaxResponseBytes, cancellationToken);
    }

    private sealed class SdkContext(ConnectorEngine engine, ConnectionDefinition connection, CredentialResolution credential, int attempt) : ISdkCallContext
    {
        public ConnectionDefinition Connection => connection;
        public ConnectionCredential? Credential => credential.Credential;
        public CredentialOwner? Owner => credential.Owner;
        public int Attempt => attempt;
        public HttpClient HttpClient => engine.Http.For(connection).Client;
        public HttpMessageHandler Handler => engine.Http.For(connection).Handler;
        public IServiceProvider Services => engine.Services;

        public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            AuthSchemes.IsOAuth2(connection.Auth.Scheme)
                ? await engine.Tokens.GetAccessTokenAsync(connection, credential.Owner, credential.Credential, forceRefresh: false, cancellationToken)
                : null;
    }

    /// <summary>Applies the connection's auth scheme with the resolved credential.</summary>
    internal async Task AuthorizeAsync(ConnectionDefinition connection, CredentialResolution resolution, HttpRequestMessage message, CancellationToken cancellationToken)
    {
        var credential = resolution.Credential;
        switch (connection.Auth.Scheme)
        {
            case AuthSchemes.None or AuthSchemes.Custom:
                return;
            case AuthSchemes.ApiKey:
                var key = credential?[CredentialKeys.ApiKey] ?? credential?[CredentialKeys.Token] ?? throw new ConnectorNotAuthorizedException($"Connection '{connection.Key}' has no API key.");
                var name = connection.Auth.KeyName ?? "X-Api-Key";
                if (connection.Auth.KeyLocation == ApiKeyLocation.Query)
                {
                    var builder = new UriBuilder(message.RequestUri!);
                    var pair = $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(key)}";
                    builder.Query = string.IsNullOrEmpty(builder.Query) || builder.Query == "?" ? pair : builder.Query.TrimStart('?') + "&" + pair;
                    message.RequestUri = builder.Uri;
                }
                else
                {
                    message.Headers.Remove(name);
                    message.Headers.TryAddWithoutValidation(name, key);
                }

                return;
            case AuthSchemes.Basic:
                var user = credential?[CredentialKeys.Username] ?? connection.Auth.Settings.GetValueOrDefault(CredentialKeys.Username) ?? string.Empty;
                var password = credential?[CredentialKeys.Password] ?? credential?[CredentialKeys.ApiKey] ?? throw new ConnectorNotAuthorizedException($"Connection '{connection.Key}' has no password.");
                message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
                return;
            case AuthSchemes.Bearer:
                var token = credential?[CredentialKeys.Token] ?? credential?[CredentialKeys.ApiKey] ?? throw new ConnectorNotAuthorizedException($"Connection '{connection.Key}' has no token.");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return;
            case AuthSchemes.OAuth2ClientCredentials or AuthSchemes.OAuth2AuthorizationCode or AuthSchemes.OAuth2DeviceCode:
                var access = await Tokens.GetAccessTokenAsync(connection, resolution.Owner, credential, forceRefresh: false, cancellationToken);
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
                return;
            case AuthSchemes.Hmac:
                throw new ConnectorRefusedException($"Connection '{connection.Key}' is for inbound webhooks (hmac); it cannot make calls.");
            default:
                throw new ConnectorRefusedException($"Unknown auth scheme '{connection.Auth.Scheme}'.");
        }
    }
}
