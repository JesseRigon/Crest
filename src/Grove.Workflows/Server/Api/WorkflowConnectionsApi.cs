using System.Security.Claims;
using System.Text.Json;
using Grove.Access;
using Grove.Api;
using Grove.Routing;
using Grove.Security;
using Grove.Workflows.Connectors;
using Grove.Workflows.Registry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Grove.Workflows.Api;

/// <summary>
/// <c>api/grove/workflows/connections</c>: the tenant's connections (secrets go in, never out),
/// a connection from an OpenAPI document, and the OAuth2 authorization-code dance for a
/// connection (docs/workflows.md › Connectors): a user who may manage connections starts it at
/// <c>{key}/oauth/authorize</c> (a redirect to the provider with a sealed state), the provider
/// sends them back to <c>oauth/callback</c>, the code is exchanged for tokens that are sealed on
/// the connection. The browser never sees a token; the state binds the callback to this tenant,
/// connection and user. The two OAuth endpoints are browser navigations (GET), outside the
/// group's antiforgery check.
/// </summary>
public sealed class WorkflowConnectionsApi : ApiContributor
{
    public override string Name => "WorkflowConnections";

    public override string FeatureId => WorkflowsConstants.FeatureId;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("workflows/connections");
        routes.MapGet("", ApiHandlers.Handle<WorkflowConnectionsHandlers>(h => h.ListAsync()));
        routes.MapGet("{key}", (HttpContext http, [FromRoute] string key) => ApiHandlers.For<WorkflowConnectionsHandlers>(http).GetAsync(key));
        routes.MapPost("", (HttpContext http, [FromBody] WorkflowConnectionSaveRequest request) => ApiHandlers.For<WorkflowConnectionsHandlers>(http).CreateAsync(request));
        routes.MapPut("{key}", (HttpContext http, [FromRoute] string key, [FromBody] WorkflowConnectionSaveRequest request) => ApiHandlers.For<WorkflowConnectionsHandlers>(http).UpdateAsync(key, request));
        routes.MapDelete("{key}", (HttpContext http, [FromRoute] string key) => ApiHandlers.For<WorkflowConnectionsHandlers>(http).DeleteAsync(key));
        routes.MapPost(WorkflowsConstants.Routes.ConnectionOpenApiImportApi, (HttpContext http, [FromBody] WorkflowConnectionOpenApiImportRequest request) =>
            ApiHandlers.For<WorkflowConnectionsHandlers>(http).ImportOpenApiAsync(request));

        routes.MapGet(WorkflowsConstants.Routes.ConnectionOAuthAuthorizeApi, (HttpContext http, [FromRoute] string key, [FromQuery] string? returnUrl) =>
            ApiHandlers.For<WorkflowConnectionOAuthHandlers>(http).AuthorizeAsync(key, returnUrl))
            .SkipApiAntiforgery();
        routes.MapGet(WorkflowsConstants.Routes.ConnectionOAuthCallbackApi, (
            HttpContext http,
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            [FromQuery(Name = "error_description")] string? errorDescription) =>
            ApiHandlers.For<WorkflowConnectionOAuthHandlers>(http).CallbackAsync(code, state, error, errorDescription))
            .SkipApiAntiforgery();
    }
}

public sealed class WorkflowConnectionsHandlers(
    IAuthorizationService authorizationService,
    WorkflowConnectionService connections,
    WorkflowRegistryCatalog catalog,
    IAccessDecision decision,
    ICallerContextAccessor callers,
    RoleManager<IRole> roleManager) : ApiHandlers
{
    private const string AssignRoleToUsersPermission = "AssignRoleToUsers";

    public async Task<IResult> ListAsync()
    {
        if (!await CanManageAsync()) return Forbid();
        return Ok((await connections.ListAsync()).Select(connections.ToModel).ToList());
    }

    public async Task<IResult> GetAsync(string key)
    {
        if (!await CanManageAsync()) return Forbid();
        var connection = await connections.FindAsync(key);
        return connection is null ? NotFound() : Ok(connections.ToModel(connection));
    }

    public async Task<IResult> CreateAsync(WorkflowConnectionSaveRequest request)
    {
        if (!await CanManageAsync()) return Forbid();
        WorkflowConnectorDescriptor? connector = null;
        if (!string.IsNullOrWhiteSpace(request.ConnectorKey) && (connector = catalog.FindConnector(request.ConnectorKey)) is null)
        {
            return Invalid($"No connector '{request.ConnectorKey}' is registered.");
        }

        if (await RoleRefusalAsync(request.Roles, []) is { } refusal) return Refused(refusal);
        var (connection, errors) = await connections.SaveAsync(null, request, connector);
        return connection is null ? Invalid(string.Join(" ", errors)) : Ok(connections.ToModel(connection));
    }

    public async Task<IResult> UpdateAsync(string key, WorkflowConnectionSaveRequest request)
    {
        if (!await CanManageAsync()) return Forbid();
        if (await connections.FindAsync(key) is not { } existing) return NotFound();
        if (await RoleRefusalAsync(request.Roles, existing.Roles) is { } refusal) return Refused(refusal);
        var (connection, errors) = await connections.SaveAsync(key, request, null);
        return connection is null ? Invalid(string.Join(" ", errors)) : Ok(connections.ToModel(connection));
    }

    public async Task<IResult> DeleteAsync(string key)
    {
        if (!await CanManageAsync()) return Forbid();
        return await connections.DeleteAsync(key) ? NoContent() : NotFound();
    }

    /// <summary>A connection from an OpenAPI 3 document: server, security scheme and operations (one palette entry each).</summary>
    public async Task<IResult> ImportOpenApiAsync(WorkflowConnectionOpenApiImportRequest request)
    {
        if (!await CanManageAsync()) return Forbid();
        OpenApiImportResult import;
        try
        {
            import = OpenApiImport.Parse(request.Document ?? string.Empty);
        }
        catch (JsonException ex)
        {
            return Invalid($"Not an OpenAPI 3 document: {ex.Message}");
        }

        var settings = new Dictionary<string, string>(import.Settings, StringComparer.OrdinalIgnoreCase)
        {
            [WorkflowConnectionSettingKeys.Operations] = JsonSerializer.Serialize(import.Operations, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        };
        var (connection, errors) = await connections.SaveAsync(null, new WorkflowConnectionSaveRequest(
            request.Key, request.DisplayName ?? import.Title, null, import.BaseUrl, import.AuthKind, settings, request.Secret, null, null, null), null);
        return connection is null
            ? Invalid(string.Join(" ", errors.Concat(import.Warnings)))
            : Ok(new { Connection = connections.ToModel(connection), import.Warnings });
    }

    private async Task<bool> CanManageAsync() => await authorizationService.AuthorizeAsync(User, Permissions.ManageConnections);

    /// <summary>
    /// Assigning a role to a connection's machine identity is a rights write: each role added
    /// takes the right to assign that role (<c>AssignRoleToUsers</c> against the role), as it
    /// does for a user, so managing connections never grants more than the manager may give.
    /// </summary>
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

    private static IResult Refused(string detail) => StatusCode(StatusCodes.Status403Forbidden, new { title = "Forbidden.", detail });

    private static IResult Invalid(string detail) => BadRequest(new { title = "Invalid connection.", detail });
}

public sealed class WorkflowConnectionOAuthHandlers(
    IAuthorizationService authorizationService,
    WorkflowConnectionService connections,
    ConnectorOAuthClient oauth,
    IDataProtectionProvider dataProtection,
    IShellLinkGenerator links,
    ILogger<WorkflowConnectionOAuthHandlers> logger) : ApiHandlers
{
    private const string StatePurpose = "Grove.Workflows.Connections.OAuthState";

    private sealed record OAuthState(string Key, string UserId, string? ReturnUrl, DateTime IssuedUtc);

    public async Task<IResult> AuthorizeAsync(string key, string? returnUrl)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ManageConnections))
        {
            return Forbid();
        }

        var connection = await connections.FindAsync(key);
        if (connection is null)
        {
            return NotFound();
        }

        if (connection.AuthKind != WorkflowConnectorAuthKinds.OAuth2AuthorizationCode)
        {
            return BadRequest(new { title = "Not an authorization-code connection.", detail = $"Connection '{key}' uses auth kind '{connection.AuthKind}'." });
        }

        var state = dataProtection.CreateProtector(StatePurpose).Protect(JsonSerializer.Serialize(new OAuthState(connection.Key, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? string.Empty, links.IsLocalUrl(returnUrl) ? returnUrl : null, DateTime.UtcNow)));
        var url = ConnectorOAuthClient.BuildAuthorizeUrl(connection, CallbackUrl(), state);
        return Results.Redirect(url);
    }

    /// <summary>The provider's redirect target. The signed-in user must be the one who started; the state must be fresh.</summary>
    public async Task<IResult> CallbackAsync(string? code, string? state, string? error, string? errorDescription)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ManageConnections))
        {
            return Forbid();
        }

        OAuthState? parsed;
        try
        {
            parsed = string.IsNullOrEmpty(state) ? null : JsonSerializer.Deserialize<OAuthState>(dataProtection.CreateProtector(StatePurpose).Unprotect(state));
        }
        catch (Exception ex) when (ex is JsonException or System.Security.Cryptography.CryptographicException)
        {
            parsed = null;
        }

        if (parsed is null || DateTime.UtcNow - parsed.IssuedUtc > TimeSpan.FromMinutes(15))
        {
            return BadRequest(new { title = "Invalid or expired state." });
        }

        var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? string.Empty;
        if (!string.Equals(currentUser, parsed.UserId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
        {
            logger.LogWarning("OAuth authorization for connection {Key} refused by the provider: {Error} {Description}", parsed.Key, error, errorDescription);
            return BadRequest(new { title = "The provider refused the authorization.", detail = $"{error}: {errorDescription}" });
        }

        var connection = await connections.FindAsync(parsed.Key);
        if (connection is null)
        {
            return NotFound();
        }

        var (tokens, exchangeError) = await oauth.ExchangeCodeAsync(connection, code, CallbackUrl(), HttpContext.RequestAborted);
        if (tokens is null)
        {
            return BadRequest(new { title = "The token exchange failed.", detail = exchangeError });
        }

        await connections.StoreTokensAsync(connection.Key, tokens);
        if (parsed.ReturnUrl is not null)
        {
            return Results.LocalRedirect(parsed.ReturnUrl);
        }

        return Ok(new { connection.Key, Authorized = true, tokens.ExpiresUtc });
    }

    /// <summary>The callback as the provider must call it: this tenant's base (prefix included) plus the callback route.</summary>
    private string CallbackUrl() => $"{Request.Scheme}://{Request.Host}{links.Content("~/")}{WorkflowsConstants.Routes.ConnectionsApi}/{WorkflowsConstants.Routes.ConnectionOAuthCallbackApi}";
}
