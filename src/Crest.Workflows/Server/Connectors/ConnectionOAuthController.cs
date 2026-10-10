using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Crest.Workflows.Connectors;

/// <summary>
/// OAuth2 authorization code for a connection (docs/workflows.md › Connectors): a user who
/// may manage connections starts the dance at <c>{key}/oauth/authorize</c> (a redirect to
/// the provider with a sealed state), the provider sends them back to <c>oauth/callback</c>,
/// the code is exchanged for tokens that are sealed on the connection. The browser never
/// sees a token; the state binds the callback to this tenant, connection and user.
/// </summary>
[ApiController, Route(WorkflowsConstants.Routes.ConnectionsApi)]
public sealed class ConnectionOAuthController(
    IAuthorizationService authorizationService,
    WorkflowConnectionService connections,
    ConnectorOAuthClient oauth,
    IDataProtectionProvider dataProtection,
    ILogger<ConnectionOAuthController> logger) : ControllerBase
{
    private const string StatePurpose = "Crest.Workflows.Connections.OAuthState";
    private sealed record OAuthState(string Key, string UserId, string? ReturnUrl, DateTime IssuedUtc);

    [HttpGet(WorkflowsConstants.Routes.ConnectionOAuthAuthorizeApi)]
    public async Task<IActionResult> AuthorizeAsync(string key, [FromQuery] string? returnUrl)
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

        var state = dataProtection.CreateProtector(StatePurpose).Protect(JsonSerializer.Serialize(new OAuthState(connection.Key, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? string.Empty, Url.IsLocalUrl(returnUrl) ? returnUrl : null, DateTime.UtcNow)));
        var url = ConnectorOAuthClient.BuildAuthorizeUrl(connection, CallbackUrl(), state);
        return Redirect(url);
    }

    /// <summary>The provider's redirect target. The signed-in user must be the one who started; the state must be fresh.</summary>
    [HttpGet(WorkflowsConstants.Routes.ConnectionOAuthCallbackApi)]
    public async Task<IActionResult> CallbackAsync([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, [FromQuery(Name = "error_description")] string? errorDescription)
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
            return LocalRedirect(parsed.ReturnUrl);
        }

        return Ok(new { connection.Key, Authorized = true, tokens.ExpiresUtc });
    }

    /// <summary>The callback as the provider must call it: this tenant's base (prefix included) plus the route the attribute above registers.</summary>
    private string CallbackUrl() => $"{Request.Scheme}://{Request.Host}{Url.Content("~/")}{WorkflowsConstants.Routes.ConnectionsApi}/{WorkflowsConstants.Routes.ConnectionOAuthCallbackApi}";
}
