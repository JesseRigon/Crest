using Grove.Access;
using Grove.Api;
using Grove.Data.Connectors.Services;
using Grove.Infrastructure.Shells.Admin;
using Grove.Routing;
using Grove.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Grove.Data.Connectors.Api;

/// <summary>
/// <c>api/grove/connections</c>: the tenant's connections, declared and defined (definitions,
/// schema import, operations, tenant credentials, app credentials, users' credentials revoked),
/// each staff user's connected accounts (<c>me</c>), and OAuth (authorize, callback, device
/// code). Admin-side callers only: a member-shell or site caller is refused. Secrets go in and
/// never come out. The OAuth callback is a browser navigation from the provider to the admin
/// shell's path (<c>{admin}/api/grove/connections/oauth/callback</c>), outside the antiforgery
/// check; the state binds it to the tenant, the connection, the scope and the user.
/// </summary>
public sealed class ConnectionsApi : ApiContributor
{
    public override string Name => "Connections";

    public override string FeatureId => ConnectorsConstants.FeatureId;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("connections");
        routes.MapGet("", ApiHandlers.Handle<ConnectionsHandlers>(h => h.ListAsync()));
        routes.MapGet("protocols", ApiHandlers.Handle<ConnectionsHandlers>(h => h.ProtocolsAsync()));
        routes.MapPost("", (HttpContext http, [FromBody] ConnectionSaveRequest request) => ApiHandlers.For<ConnectionsHandlers>(http).SaveAsync(null, request));
        routes.MapPost("import", (HttpContext http, [FromBody] SchemaImportRequest request) => ApiHandlers.For<ConnectionsHandlers>(http).ImportAsync(request));
        routes.MapGet("{key}", (HttpContext http, [FromRoute] string key) => ApiHandlers.For<ConnectionsHandlers>(http).GetAsync(key));
        routes.MapPut("{key}", (HttpContext http, [FromRoute] string key, [FromBody] ConnectionSaveRequest request) => ApiHandlers.For<ConnectionsHandlers>(http).SaveAsync(key, request));
        routes.MapDelete("{key}", (HttpContext http, [FromRoute] string key) => ApiHandlers.For<ConnectionsHandlers>(http).DeleteAsync(key));
        routes.MapPut("{key}/credential", (HttpContext http, [FromRoute] string key, [FromBody] CredentialSaveRequest request) => ApiHandlers.For<ConnectionsHandlers>(http).SaveTenantCredentialAsync(key, request));
        routes.MapDelete("{key}/credential", (HttpContext http, [FromRoute] string key) => ApiHandlers.For<ConnectionsHandlers>(http).RevokeAsync(key, CredentialScope.Tenant.Value, null));
        routes.MapDelete("{key}/credentials/{userId}", (HttpContext http, [FromRoute] string key, [FromRoute] string userId) => ApiHandlers.For<ConnectionsHandlers>(http).RevokeAsync(key, CredentialScope.User.Value, userId));

        routes.MapGet("app-credentials", ApiHandlers.Handle<ConnectionsHandlers>(h => h.ListAppCredentialsAsync()));
        routes.MapPut("app-credentials/{key}", (HttpContext http, [FromRoute] string key, [FromBody] AppCredentialsSaveRequest request) => ApiHandlers.For<ConnectionsHandlers>(http).SaveAppCredentialsAsync(key, request));
        routes.MapDelete("app-credentials/{key}", (HttpContext http, [FromRoute] string key) => ApiHandlers.For<ConnectionsHandlers>(http).DeleteAppCredentialsAsync(key));

        routes.MapGet("me", ApiHandlers.Handle<ConnectionsHandlers>(h => h.MyAccountsAsync()));
        routes.MapPut("me/{key}", (HttpContext http, [FromRoute] string key, [FromBody] CredentialSaveRequest request) => ApiHandlers.For<ConnectionsHandlers>(http).SaveMyCredentialAsync(key, request));
        routes.MapDelete("me/{key}", (HttpContext http, [FromRoute] string key) => ApiHandlers.For<ConnectionsHandlers>(http).RevokeAsync(key, CredentialScope.User.Value, ""));

        routes.MapGet("{key}/oauth/authorize", (HttpContext http, [FromRoute] string key, [FromQuery] string? scope, [FromQuery] string? returnUrl) =>
            ApiHandlers.For<ConnectionsHandlers>(http).AuthorizeAsync(key, scope, returnUrl));
        routes.MapGet("oauth/callback", (
            HttpContext http,
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            [FromQuery(Name = "error_description")] string? errorDescription) =>
            ApiHandlers.For<ConnectionsHandlers>(http).CallbackAsync(code, state, error, errorDescription))
            .SkipApiAntiforgery();
        routes.MapPost("{key}/oauth/device", (HttpContext http, [FromRoute] string key, [FromQuery] string? scope) =>
            ApiHandlers.For<ConnectionsHandlers>(http).BeginDeviceAsync(key, scope));
        routes.MapPost("{key}/oauth/device/poll", (HttpContext http, [FromRoute] string key, [FromQuery] string? scope, [FromBody] DevicePollRequest request) =>
            ApiHandlers.For<ConnectionsHandlers>(http).PollDeviceAsync(key, scope, request));
    }
}

public sealed class ConnectionsHandlers(
    ICallerContextAccessor callers,
    IAccessDecision decision,
    ConnectionRegistry registry,
    DefinedConnectionStore definedStore,
    CredentialStore credentials,
    IEnumerable<IConnectorProtocol> protocols,
    IEnumerable<ConnectionDeclaration> declarations,
    IConnectionAuthorizationService authorization,
    IShellLinkGenerator links,
    IOptions<AdminOptions> adminOptions,
    RoleManager<IRole> roleManager) : ApiHandlers
{
    private const string AssignRoleToUsersPermission = "AssignRoleToUsers";
    private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase) { "me", "protocols", "import", "oauth", "app-credentials" };

    public async Task<IResult> ListAsync()
    {
        if (await ManagerAsync() is not { } refusal)
        {
            var models = new List<ConnectionModel>();
            foreach (var connection in await registry.ListAsync(HttpContext.RequestAborted))
            {
                models.Add(await ToModelAsync(connection));
            }

            return Ok(models);
        }

        return refusal;
    }

    public async Task<IResult> ProtocolsAsync()
    {
        if (await ManagerAsync() is { } refusal) return refusal;
        return Ok(protocols.Select(p => new ProtocolModel(p.Id, p.DisplayName, p.AuthSchemes, p.SchemaFormats,
            p.SettingFields.Select(ToModel).ToArray(), p.CredentialFields.Select(ToModel).ToArray(), p.DefaultOperations.Count)).OrderBy(p => p.DisplayName).ToList());
    }

    public async Task<IResult> GetAsync(string key)
    {
        if (await ManagerAsync() is { } refusal) return refusal;
        var connection = await registry.FindAsync(key, HttpContext.RequestAborted);
        return connection is null ? NotFound() : Ok(await DetailAsync(connection, []));
    }

    public async Task<IResult> SaveAsync(string? key, ConnectionSaveRequest request)
    {
        if (await ManagerAsync() is { } refusal) return refusal;

        ConnectionDefinition? existing = null;
        if (key is not null)
        {
            if (declarations.Any(d => string.Equals(d.Definition.Key, key, StringComparison.OrdinalIgnoreCase)))
            {
                return ApiProblems.Conflict($"'{key}' is declared by a module; only its credentials can be changed here.");
            }

            existing = await definedStore.FindAsync(key, HttpContext.RequestAborted);
            if (existing is null) return NotFound();
        }

        var errors = new Dictionary<string, string[]>();
        var newKey = (key ?? request.Key)?.Trim().ToLowerInvariant() ?? string.Empty;
        if (key is null)
        {
            if (!DefinedConnectionStore.IsValidKey(newKey) || ReservedKeys.Contains(newKey))
            {
                errors["key"] = ["A key is 1-64 lowercase letters, digits or dashes, starting with a letter or digit, and not a reserved word."];
            }
            else if (await registry.FindAsync(newKey, HttpContext.RequestAborted) is not null)
            {
                errors["key"] = [$"A connection '{newKey}' already exists."];
            }
        }

        var definition = Apply(existing, newKey, request);
        Validate(definition, errors);
        if (errors.Count > 0) return ApiProblems.Validation(errors);

        if (await RoleRefusalAsync(request.CallerRoles, existing?.CallerRoles ?? []) is { } roleRefusal)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { title = "Forbidden.", detail = roleRefusal });
        }

        var saved = await definedStore.SaveAsync(definition, HttpContext.RequestAborted);
        return Ok(await DetailAsync(saved, []));
    }

    public async Task<IResult> ImportAsync(SchemaImportRequest request)
    {
        if (await ManagerAsync() is { } refusal) return refusal;
        var format = request.Format?.Trim().ToLowerInvariant();
        var protocol = protocols.FirstOrDefault(p => format is not null && p.SchemaFormats.Contains(format, StringComparer.OrdinalIgnoreCase));
        if (protocol is null)
        {
            return ApiProblems.Validation("format", $"Choose a schema format: {string.Join(", ", protocols.SelectMany(p => p.SchemaFormats).Distinct())}.");
        }

        ConnectionSchemaSource source;
        if (!string.IsNullOrWhiteSpace(request.Document))
        {
            source = ConnectionSchemaSource.Inline(format!, request.Document);
        }
        else if (Uri.TryCreate(request.Url, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp))
        {
            source = ConnectionSchemaSource.FromUrl(format!, url.AbsoluteUri);
        }
        else
        {
            return ApiProblems.Validation("document", "Paste the schema or give its http(s) URL.");
        }

        var key = request.Key?.Trim().ToLowerInvariant() ?? string.Empty;
        ConnectionDefinition? existing = null;
        if (request.Replace)
        {
            existing = await definedStore.FindAsync(key, HttpContext.RequestAborted);
            if (existing is null) return NotFound();
        }
        else if (!DefinedConnectionStore.IsValidKey(key) || ReservedKeys.Contains(key))
        {
            return ApiProblems.Validation("key", "A key is 1-64 lowercase letters, digits or dashes, starting with a letter or digit.");
        }
        else if (await registry.FindAsync(key, HttpContext.RequestAborted) is not null)
        {
            return ApiProblems.Validation("key", $"A connection '{key}' already exists.");
        }

        var draft = new ConnectionDefinition
        {
            Key = key,
            DisplayName = existing?.DisplayName ?? request.DisplayName?.Trim() ?? key,
            Description = existing?.Description,
            Protocol = protocol.Id,
            BaseUrl = existing?.BaseUrl,
            Schema = source,
            Auth = existing?.Auth ?? ConnectionAuth.None,
            AllowedScopes = existing?.AllowedScopes ?? [CredentialScope.Tenant],
            Policy = existing?.Policy ?? new ConnectionPolicy(),
            Operations = existing?.Operations ?? [],
            Settings = existing?.Settings ?? new Dictionary<string, string>(),
            CallerRoles = existing?.CallerRoles ?? [],
        };

        var read = await registry.ReadSchemaAsync(draft, HttpContext.RequestAborted);
        if (read.Operations.Count == 0)
        {
            return ApiProblems.Validation("document", string.Join(" ", read.Warnings ?? ["The schema yields no operations."]));
        }

        var definition = new ConnectionDefinition
        {
            Key = draft.Key,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) && existing is null ? read.Title ?? key : draft.DisplayName,
            Description = draft.Description,
            Protocol = draft.Protocol,
            BaseUrl = draft.BaseUrl ?? read.BaseUrl,
            Schema = draft.Schema,
            Auth = existing is null ? read.Auth ?? ConnectionAuth.None : draft.Auth,
            AllowedScopes = draft.AllowedScopes,
            Policy = draft.Policy,
            Operations = draft.Operations,
            Settings = draft.Settings,
            CallerRoles = draft.CallerRoles,
        };

        var saved = await definedStore.SaveAsync(definition, HttpContext.RequestAborted);
        var warnings = read.Warnings ?? [];
        return Ok(new SchemaImportResult(await DetailAsync(saved, warnings), warnings));
    }

    public async Task<IResult> DeleteAsync(string key)
    {
        if (await ManagerAsync() is { } refusal) return refusal;
        if (declarations.Any(d => string.Equals(d.Definition.Key, key, StringComparison.OrdinalIgnoreCase)))
        {
            return ApiProblems.Conflict($"'{key}' is declared by a module; disable its feature instead.");
        }

        return await definedStore.DeleteAsync(key, HttpContext.RequestAborted) ? NoContent() : NotFound();
    }

    public async Task<IResult> SaveTenantCredentialAsync(string key, CredentialSaveRequest request)
    {
        if (await ManagerAsync() is { } refusal) return refusal;
        var connection = await registry.FindAsync(key, HttpContext.RequestAborted);
        if (connection is null) return NotFound();
        if (!connection.Allows(CredentialScope.Tenant) && !AuthSchemes.IsOAuth2(connection.Auth.Scheme))
        {
            return ApiProblems.Validation("", $"Connection '{key}' takes no tenant credential.");
        }

        var info = await credentials.SaveAsync(connection.Key, CredentialOwner.Tenant, new ConnectionCredential { Values = request.Values }, request.Label ?? string.Empty, merge: true, HttpContext.RequestAborted);
        return Ok(ToModel(info));
    }

    public async Task<IResult> RevokeAsync(string key, string scope, string? userId)
    {
        if (Staff() is not { } caller) return Forbid();
        CredentialOwner owner;
        if (scope == CredentialScope.Tenant.Value)
        {
            owner = CredentialOwner.Tenant;
        }
        else if (string.IsNullOrEmpty(userId))
        {
            owner = CredentialOwner.ForUser(caller.UserId!);
        }
        else
        {
            owner = CredentialOwner.ForUser(userId);
        }

        if (await credentials.FindAsync(key, owner, HttpContext.RequestAborted) is null) return NotFound();
        return await authorization.RevokeAsync(key, caller, owner, HttpContext.RequestAborted) ? NoContent() : Forbid();
    }

    public async Task<IResult> ListAppCredentialsAsync()
    {
        if (await ManagerAsync() is { } refusal) return refusal;
        var stored = (await credentials.ListAppCredentialsAsync()).ToDictionary(a => a.Key, StringComparer.OrdinalIgnoreCase);
        var usedBy = (await registry.ListAsync(HttpContext.RequestAborted))
            .Where(c => c.Auth.AppCredentialsKey is not null)
            .GroupBy(c => c.Auth.AppCredentialsKey!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Key).ToArray(), StringComparer.OrdinalIgnoreCase);
        var keys = stored.Keys.Concat(usedBy.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase);
        return Ok(keys.Select(k => stored.TryGetValue(k, out var s)
            ? new AppCredentialsModel(k, s.ClientId, s.HasSecret, s.UpdatedUtc, usedBy.GetValueOrDefault(k) ?? [])
            : new AppCredentialsModel(k, null, false, null, usedBy.GetValueOrDefault(k) ?? [])).ToList());
    }

    public async Task<IResult> SaveAppCredentialsAsync(string key, AppCredentialsSaveRequest request)
    {
        if (await ManagerAsync() is { } refusal) return refusal;
        if (!DefinedConnectionStore.IsValidKey(key))
        {
            return ApiProblems.Validation("key", "An app credentials key is 1-64 lowercase letters, digits or dashes.");
        }

        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return ApiProblems.Validation("clientId", "The client id is required.");
        }

        await credentials.SaveAppCredentialsAsync(key, request.ClientId.Trim(), request.ClientSecret);
        return NoContent();
    }

    public async Task<IResult> DeleteAppCredentialsAsync(string key)
    {
        if (await ManagerAsync() is { } refusal) return refusal;
        return await credentials.DeleteAppCredentialsAsync(key) ? NoContent() : NotFound();
    }

    /// <summary>The connections that take user credentials, with the caller's own.</summary>
    public async Task<IResult> MyAccountsAsync()
    {
        if (Staff() is not { } caller) return Forbid();
        var owner = CredentialOwner.ForUser(caller.UserId!);
        var mine = (await credentials.ListAsync(owner: owner, cancellationToken: HttpContext.RequestAborted)).ToDictionary(c => c.ConnectionKey, StringComparer.OrdinalIgnoreCase);
        var accounts = (await registry.ListAsync(HttpContext.RequestAborted))
            .Where(c => c.Allows(CredentialScope.User))
            .Select(c => new ConnectedAccountModel(c.Key, c.DisplayName, c.Description, c.Auth.Scheme, AuthSchemes.IsInteractive(c.Auth.Scheme), c.Policy.AllowTenantFallback,
                mine.TryGetValue(c.Key, out var info) ? ToModel(info) : null))
            .ToList();
        return Ok(accounts);
    }

    public async Task<IResult> SaveMyCredentialAsync(string key, CredentialSaveRequest request)
    {
        if (Staff() is not { } caller) return Forbid();
        var connection = await registry.FindAsync(key, HttpContext.RequestAborted);
        if (connection is null || !connection.Allows(CredentialScope.User)) return NotFound();
        var owner = CredentialOwner.ForUser(caller.UserId!);
        if (!new UserScopeHandler().CanManage(caller, owner) && !await decision.AllowsAsync(caller, ConnectorsPermissions.ConnectOwnAccounts.Name, null, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var info = await credentials.SaveAsync(connection.Key, owner, new ConnectionCredential { Values = request.Values }, request.Label ?? string.Empty, merge: true, HttpContext.RequestAborted);
        return Ok(ToModel(info));
    }

    /// <summary>Starts an authorization-code grant: answers the provider URL to send the browser to.</summary>
    public async Task<IResult> AuthorizeAsync(string key, string? scope, string? returnUrl)
    {
        if (Staff() is not { } caller) return Forbid();
        if (!CredentialScope.TryParse(scope ?? CredentialScope.User.Value, out var parsed)) return ApiProblems.Validation("scope", "Unknown scope.");
        var (url, error) = await authorization.BeginAsync(key, caller, parsed, CallbackUrl(), links.IsLocalUrl(returnUrl) ? returnUrl : null, HttpContext.RequestAborted);
        return url is null ? ApiProblems.BadRequest(error, "The authorization cannot start.") : Ok(new AuthorizationStartModel(url.AbsoluteUri));
    }

    /// <summary>The provider's redirect target: the signed-in user must be the one who started; the state must be fresh.</summary>
    public async Task<IResult> CallbackAsync(string? code, string? state, string? error, string? errorDescription)
    {
        if (Staff() is not { } caller) return Forbid();
        if (string.IsNullOrEmpty(state)) return ApiProblems.BadRequest("The authorization state is missing.");
        var result = await authorization.CompleteAsync(caller, state, code, string.IsNullOrEmpty(error) ? null : $"{error} {errorDescription}".Trim(), CallbackUrl(), HttpContext.RequestAborted);
        if (result.ReturnUrl is { } returnUrl && links.IsLocalUrl(returnUrl))
        {
            var separator = returnUrl.Contains('?') ? '&' : '?';
            return Results.LocalRedirect(result.Succeeded ? returnUrl : $"{returnUrl}{separator}connectError={Uri.EscapeDataString(result.Error ?? "failed")}");
        }

        return result.Succeeded ? Ok(new { connected = true }) : ApiProblems.BadRequest(result.Error, "The authorization failed.");
    }

    public async Task<IResult> BeginDeviceAsync(string key, string? scope)
    {
        if (Staff() is not { } caller) return Forbid();
        if (!CredentialScope.TryParse(scope ?? CredentialScope.User.Value, out var parsed)) return ApiProblems.Validation("scope", "Unknown scope.");
        var (started, error) = await authorization.BeginDeviceAsync(key, caller, parsed, HttpContext.RequestAborted);
        return started is null
            ? ApiProblems.BadRequest(error, "The device authorization cannot start.")
            : Ok(new DeviceAuthorizationModel(started.DeviceCode, started.UserCode, started.VerificationUri, started.VerificationUriComplete, started.ExpiresUtc, (int)started.Interval.TotalSeconds));
    }

    public async Task<IResult> PollDeviceAsync(string key, string? scope, DevicePollRequest request)
    {
        if (Staff() is not { } caller) return Forbid();
        if (!CredentialScope.TryParse(scope ?? CredentialScope.User.Value, out var parsed)) return ApiProblems.Validation("scope", "Unknown scope.");
        if (string.IsNullOrWhiteSpace(request.DeviceCode)) return ApiProblems.Validation("deviceCode", "The device code is required.");
        var result = await authorization.PollDeviceAsync(key, caller, parsed, request.DeviceCode, HttpContext.RequestAborted);
        return Ok(new DevicePollResult(result.Succeeded, !result.Succeeded && result.Error == "pending", result.Succeeded || result.Error == "pending" ? null : result.Error));
    }

    private CallerContext? Staff() => callers.Current is { } caller && ConnectorCallers.IsStaffUser(caller) ? caller : null;

    /// <summary>Null when the caller is a staff user who manages connections, else the refusal.</summary>
    private async Task<IResult?> ManagerAsync()
    {
        if (Staff() is not { } caller)
        {
            return Forbid();
        }

        return await decision.AllowsAsync(caller, ConnectorsPermissions.ManageConnections.Name, null, HttpContext.RequestAborted) ? null : Forbid();
    }

    /// <summary>The callback as the provider calls it: the tenant's base, the admin shell's prefix, the route.</summary>
    private string CallbackUrl() =>
        $"{Request.Scheme}://{Request.Host}{Request.PathBase}/{adminOptions.Value.AdminUrlPrefix}/{ConnectorsConstants.OAuthCallbackRoute}";

    private async Task<ConnectionModel> ToModelAsync(ConnectionDefinition c)
    {
        var all = await credentials.ListAsync(c.Key, cancellationToken: HttpContext.RequestAborted);
        var tenant = all.FirstOrDefault(i => i.Owner.Scope == CredentialScope.Tenant);
        return new ConnectionModel(
            c.Key, c.DisplayName, c.Description, c.Protocol, c.Origin.ToString().ToLowerInvariant(), c.FeatureId, c.BaseUrl, c.Endpoints,
            new ConnectionAuthModel(c.Auth.Scheme, c.Auth.KeyName, c.Auth.KeyLocation.ToString().ToLowerInvariant(), c.Auth.AuthorizeUrl, c.Auth.TokenUrl, c.Auth.DeviceAuthorizationUrl,
                c.Auth.RevokeUrl, c.Auth.Scopes, c.Auth.UsePkce, c.Auth.AppCredentialsKey, c.Auth.SignatureScheme, c.Auth.SignatureHeader),
            c.AllowedScopes.Select(s => s.Value).ToArray(),
            new ConnectionPolicyModel(c.Policy.RetryCount, (int)c.Policy.Timeout.TotalSeconds, c.Policy.RateLimit?.Permits, c.Policy.RateLimit is { } r ? (int)r.Window.TotalSeconds : null,
                c.Policy.Quota?.Limit, c.Policy.Quota is { } q ? (int)Math.Max(1, q.Period.TotalHours) : null, c.Policy.CacheLifetime is { } l ? (int)l.TotalSeconds : null,
                c.Policy.MaxResponseBytes, c.Policy.MaxRequestBytes, (int)c.Policy.MaxInlineWait.TotalSeconds, c.Policy.AllowTenantFallback),
            c.Settings, c.CallerRoles, c.Schema?.Format, c.Schema?.Kind.ToString().ToLowerInvariant(), c.Schema?.Kind == ConnectionSchemaSourceKind.Inline ? null : c.Schema?.Location,
            tenant is null ? null : ToModel(tenant), all.Count(i => i.Owner.Scope == CredentialScope.User));
    }

    private async Task<ConnectionDetailModel> DetailAsync(ConnectionDefinition connection, IReadOnlyList<string> warnings)
    {
        var operations = await registry.GetOperationsAsync(connection.Key, HttpContext.RequestAborted);
        var users = (await credentials.ListAsync(connection.Key, cancellationToken: HttpContext.RequestAborted)).Where(i => i.Owner.Scope == CredentialScope.User).Select(ToModel).ToList();
        return new ConnectionDetailModel(await ToModelAsync(connection), operations.Select(o => new OperationModel(o.Id, o.DisplayName, o.Description, o.Kind, o.Method, o.Path,
            o.IsSafeToRetry, o.SupportsIdempotencyKey, o.Parameters.Select(p => $"{p.Name} ({p.In.ToString().ToLowerInvariant()}{(p.Required ? ", required" : string.Empty)})").ToArray(),
            o.Paging is { } paging ? paging.Style.ToString().ToLowerInvariant() : null)).ToList(), warnings, users);
    }

    private static SettingFieldModel ToModel(ConnectionSettingField field) =>
        new(field.Name, field.Label, field.Type, field.Required, field.Default, field.Description, field.Options);

    private static CredentialInfoModel ToModel(CredentialInfo info) => new(info.ConnectionKey, info.Owner.Scope.Value, info.Owner.OwnerId, info.Label, info.AccountName,
        info.ValueNames, info.GrantedScopes, info.UpdatedUtc, info.ExpiresUtc, info.Status.ToString().ToLowerInvariant());

    private static ConnectionDefinition Apply(ConnectionDefinition? existing, string key, ConnectionSaveRequest request)
    {
        var auth = request.Auth is { } a
            ? new ConnectionAuth
            {
                Scheme = string.IsNullOrWhiteSpace(a.Scheme) ? AuthSchemes.None : a.Scheme.Trim().ToLowerInvariant(),
                KeyName = Blank(a.KeyName),
                KeyLocation = string.Equals(a.KeyLocation, "query", StringComparison.OrdinalIgnoreCase) ? ApiKeyLocation.Query : ApiKeyLocation.Header,
                AuthorizeUrl = Blank(a.AuthorizeUrl),
                TokenUrl = Blank(a.TokenUrl),
                DeviceAuthorizationUrl = Blank(a.DeviceAuthorizationUrl),
                RevokeUrl = Blank(a.RevokeUrl),
                Scopes = a.Scopes?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray() ?? [],
                UsePkce = a.UsePkce,
                AppCredentialsKey = Blank(a.AppCredentialsKey),
                SignatureScheme = Blank(a.SignatureScheme),
                SignatureHeader = Blank(a.SignatureHeader),
                Settings = existing?.Auth.Settings ?? new Dictionary<string, string>(),
            }
            : existing?.Auth ?? ConnectionAuth.None;
        var policy = request.Policy is { } p
            ? new Models.ConnectionPolicyRecord
            {
                RetryCount = p.RetryCount,
                TimeoutSeconds = p.TimeoutSeconds,
                RateLimitPermits = p.RateLimitPermits is > 0 ? p.RateLimitPermits : null,
                RateLimitWindowSeconds = p.RateLimitWindowSeconds,
                QuotaLimit = p.QuotaLimit is > 0 ? p.QuotaLimit : null,
                QuotaPeriodHours = p.QuotaPeriodHours,
                CacheSeconds = p.CacheSeconds is > 0 ? p.CacheSeconds : null,
                MaxResponseBytes = p.MaxResponseBytes,
                MaxRequestBytes = p.MaxRequestBytes,
                MaxInlineWaitSeconds = p.MaxInlineWaitSeconds,
                AllowTenantFallback = p.AllowTenantFallback,
            }.ToPolicy()
            : existing?.Policy ?? new ConnectionPolicy();

        return new ConnectionDefinition
        {
            Key = key,
            DisplayName = request.DisplayName?.Trim() is { Length: > 0 } name ? name : existing?.DisplayName ?? key,
            Description = request.Description is null ? existing?.Description : Blank(request.Description),
            Protocol = request.Protocol?.Trim().ToLowerInvariant() ?? existing?.Protocol ?? ConnectorProtocols.Http,
            BaseUrl = request.BaseUrl is null ? existing?.BaseUrl : Blank(request.BaseUrl),
            Endpoints = request.Endpoints ?? existing?.Endpoints ?? new Dictionary<string, string>(),
            Schema = existing?.Schema,
            Auth = auth,
            AllowedScopes = request.AllowedScopes is { Count: > 0 } scopes
                ? scopes.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => new CredentialScope(s.Trim())).Distinct().ToArray()
                : existing?.AllowedScopes ?? [CredentialScope.Tenant],
            Policy = policy,
            Operations = request.Operations is { } operations
                ? operations.Select(o => new ConnectionOperation
                {
                    Id = o.Id.Trim(),
                    DisplayName = Blank(o.DisplayName),
                    Kind = string.IsNullOrWhiteSpace(o.Kind) ? OperationKinds.Http : o.Kind,
                    Method = Blank(o.Method)?.ToUpperInvariant(),
                    Path = o.Path,
                    IsSafeToRetry = o.IsSafeToRetry,
                    SupportsIdempotencyKey = o.SupportsIdempotencyKey,
                }).ToArray()
                : existing?.Operations ?? [],
            Settings = request.Settings ?? existing?.Settings ?? new Dictionary<string, string>(),
            CallerRoles = request.CallerRoles?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? existing?.CallerRoles ?? [],
        };
    }

    private void Validate(ConnectionDefinition c, Dictionary<string, string[]> errors)
    {
        var protocol = protocols.FirstOrDefault(p => string.Equals(p.Id, c.Protocol, StringComparison.OrdinalIgnoreCase));
        if (protocol is null)
        {
            errors["protocol"] = [$"Unknown protocol '{c.Protocol}'. One of: {string.Join(", ", protocols.Select(p => p.Id))}."];
            return;
        }

        if (!protocol.AuthSchemes.Contains(c.Auth.Scheme))
        {
            errors["auth.scheme"] = [$"The {protocol.DisplayName} protocol carries: {string.Join(", ", protocol.AuthSchemes)}."];
        }

        // HTTP-based protocols need a base URL; a protocol that describes its own settings (SQL,
        // mail) finds its endpoint there, and an inbound webhook has none.
        var needsUrl = protocol.SettingFields.Count == 0 && c.Protocol != ConnectorProtocols.Webhook;
        foreach (var field in protocol.SettingFields.Where(f => f.Required))
        {
            if (string.IsNullOrWhiteSpace(c.Setting(field.Name)) && string.IsNullOrWhiteSpace(field.Default))
            {
                errors[$"settings.{field.Name}"] = [$"{field.Label} is required."];
            }
        }

        if (needsUrl && !IsHttpUrl(c.BaseUrl))
        {
            errors["baseUrl"] = ["The base URL must be an absolute http or https URL."];
        }
        else if (c.BaseUrl is not null && !needsUrl && !IsHttpUrl(c.BaseUrl))
        {
            errors["baseUrl"] = ["The base URL must be an absolute http or https URL, or empty."];
        }

        if (AuthSchemes.IsOAuth2(c.Auth.Scheme) && !IsHttpUrl(c.Auth.TokenUrl))
        {
            errors["auth.tokenUrl"] = ["OAuth 2 needs a token URL (absolute http or https)."];
        }

        if (c.Auth.Scheme == AuthSchemes.OAuth2AuthorizationCode && !IsHttpUrl(c.Auth.AuthorizeUrl))
        {
            errors["auth.authorizeUrl"] = ["The authorization code flow needs an authorization URL (absolute http or https)."];
        }

        if (c.Auth.Scheme == AuthSchemes.OAuth2DeviceCode && !IsHttpUrl(c.Auth.DeviceAuthorizationUrl))
        {
            errors["auth.deviceAuthorizationUrl"] = ["The device code flow needs a device authorization URL (absolute http or https)."];
        }

        foreach (var scope in c.AllowedScopes)
        {
            if (scope != CredentialScope.Tenant && scope != CredentialScope.User)
            {
                errors["allowedScopes"] = [$"Unknown credential scope '{scope.Value}' (tenant, user)."];
            }
        }

        if (c.Auth.Scheme == AuthSchemes.Hmac && c.AllowedScopes.Any(s => s != CredentialScope.Tenant))
        {
            errors["allowedScopes"] = ["A webhook connection holds one tenant secret."];
        }

        if (c.Policy.RetryCount is < 0 or > 10) errors["policy.retryCount"] = ["Retries must be 0-10."];
        if (c.Policy.Timeout.TotalSeconds is < 1 or > 300) errors["policy.timeoutSeconds"] = ["The timeout must be 1-300 seconds."];
        if (c.Policy.MaxInlineWait.TotalSeconds is < 0 or > 60) errors["policy.maxInlineWaitSeconds"] = ["The longest inline wait is 0-60 seconds."];
        if (c.Policy.MaxResponseBytes is < 1024 or > 100L * 1024 * 1024) errors["policy.maxResponseBytes"] = ["The response cap must be 1 KB-100 MB."];
        if (c.Operations.GroupBy(o => o.Id).Any(g => g.Key.Length == 0 || g.Count() > 1)) errors["operations"] = ["Each operation needs a unique id."];
    }

    /// <summary>Each role added to a connection's machine identity takes the right to assign that role to users.</summary>
    private async Task<string?> RoleRefusalAsync(IReadOnlyList<string>? requested, IReadOnlyList<string> current)
    {
        if (requested is null)
        {
            return null;
        }

        foreach (var roleName in requested.Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => role.Trim()).Except(current, StringComparer.OrdinalIgnoreCase))
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                return $"No role '{roleName}'.";
            }

            if (!await decision.AllowsAsync(callers.Current, AssignRoleToUsersPermission, role, HttpContext.RequestAborted))
            {
                return $"Assigning role '{roleName}' to a connection takes the right to assign it to users.";
            }
        }

        return null;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && string.IsNullOrEmpty(uri.UserInfo);
}
