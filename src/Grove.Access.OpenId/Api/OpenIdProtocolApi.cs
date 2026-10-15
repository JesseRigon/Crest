using System.Collections.Immutable;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Grove.Api;
using Grove.Environment.Shell;
using Grove.Access.OpenId.Abstractions.Managers;
using Grove.Access.OpenId.Services;
using Grove.Security.RateLimits;
using Grove.Routing;
using Grove.Security.Services;
using Grove.Access.Users;
using Grove.Access.Users.Services;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grove.Access.OpenId.Api;

/// <summary>
/// The OpenID Connect server's protocol endpoints (authorization, logout, token, userinfo), at the
/// paths the server settings name (<c>/connect/*</c>): OpenIddict extracts and validates the
/// request in its own middleware and passes it through to these endpoints, which answer with
/// sign-in, forbid, challenge and sign-out results on OpenIddict's scheme. Mapped only when the
/// server settings are valid (as OpenIdServerConfiguration registers the server handler only
/// then). None of them carries the API antiforgery check: the authorization and logout requests
/// come from other sites and the token and userinfo endpoints from machines; the consent and
/// logout confirmations (the site's OpenIdConsent page, a form post with the antiforgery field)
/// are validated by the handler.
/// </summary>
public sealed class OpenIdProtocolApi(IOpenIdServerService serverService) : IApiContributor
{
    public string Name => "OpenIdProtocol";

    public string FeatureId => OpenIdConstants.Features.Server;

    public string RoutePrefix => string.Empty;

    public void Map(IEndpointRouteBuilder group)
    {
        // The paths are settings; the mapping runs while the tenant's pipeline is built, where the
        // settings service (a singleton over the site settings) is safe to wait on.
        var settings = serverService.GetSettingsAsync().GetAwaiter().GetResult();
        if (serverService.ValidateSettingsAsync(settings).GetAwaiter().GetResult().Any(result => result != System.ComponentModel.DataAnnotations.ValidationResult.Success))
        {
            return;
        }

        string[] getOrPost = [HttpMethods.Get, HttpMethods.Post];
        var disableCors = new DisableCorsAttribute();

        if (settings.AuthorizationEndpointPath.HasValue)
        {
            group.MapMethods(settings.AuthorizationEndpointPath.Value, getOrPost, ApiHandlers.Handle<OpenIdProtocolHandlers>(h => h.AuthorizeEndpointAsync()))
                .WithName("Access.Authorize")
                .AllowAnonymous()
                .WithMetadata(disableCors)
                .SkipApiAntiforgery();
        }

        if (settings.LogoutEndpointPath.HasValue)
        {
            group.MapMethods(settings.LogoutEndpointPath.Value, getOrPost, ApiHandlers.Handle<OpenIdProtocolHandlers>(h => h.LogoutEndpointAsync()))
                .WithName("Access.Logout")
                .AllowAnonymous()
                .WithMetadata(disableCors)
                .SkipApiAntiforgery();
        }

        if (settings.TokenEndpointPath.HasValue)
        {
            group.MapPost(settings.TokenEndpointPath.Value, ApiHandlers.Handle<OpenIdProtocolHandlers>(h => h.Token()))
                .WithName("Access.Token")
                .AllowAnonymous()
                .WithRateLimitGroup(UserRateLimiterPolicyNames.PasswordAuthentication)
                .SkipApiAntiforgery();
        }

        if (settings.UserinfoEndpointPath.HasValue)
        {
            // The handler authenticates against OpenIddict's server scheme itself (as the controller
            // did) rather than through an authorization policy: it answers the protocol's own
            // challenge and forbid, and the scheme exists only while the server settings are valid.
            group.MapMethods(settings.UserinfoEndpointPath.Value, getOrPost, ApiHandlers.Handle<OpenIdUserInfoHandlers>(h => h.MeAsync()))
                .WithName("UserInfo.Me")
                .AllowAnonymous()
                .WithMetadata(SkipStatusCodePages.Instance)
                .SkipApiAntiforgery();
        }
    }

    private sealed class SkipStatusCodePages : Microsoft.AspNetCore.Http.Metadata.ISkipStatusCodePagesMetadata
    {
        public static readonly SkipStatusCodePages Instance = new();
    }
}

// Note: the error descriptions used in these handlers are deliberately not localized as
// the OAuth 2.0 specification only allows select US-ASCII characters in error_description.
public sealed class OpenIdProtocolHandlers : ApiHandlers
{
    /// <summary>
    /// The marker that replaces the controller's TempData flag: set when the authorization
    /// endpoint challenged the user agent to re-authenticate (prompt=login, max_age), read and
    /// cleared on its way back, so the endpoint does not challenge again in a loop. A short-lived,
    /// data-protected cookie scoped to the authorization endpoint's path.
    /// </summary>
    private const string IgnoreChallengeCookie = ".OpenId.IgnoreAuthenticationChallenge";
    private const string IgnoreChallengePurpose = "Grove.Access.OpenId.IgnoreAuthenticationChallenge";
    private static readonly TimeSpan s_ignoreChallengeLifetime = TimeSpan.FromMinutes(15);

    private readonly IOpenIdApplicationManager _applicationManager;
    private readonly IOpenIdAuthorizationManager _authorizationManager;
    private readonly IOpenIdScopeManager _scopeManager;
    private readonly IOpenIdServerService _serverService;
    private readonly ShellSettings _shellSettings;
    private readonly IAntiforgery _antiforgery;
    private readonly IDataProtectionProvider _dataProtection;

    public OpenIdProtocolHandlers(
        IOpenIdApplicationManager applicationManager,
        IOpenIdAuthorizationManager authorizationManager,
        IOpenIdScopeManager scopeManager,
        IOpenIdServerService serverService,
        ShellSettings shellSettings,
        IAntiforgery antiforgery,
        IDataProtectionProvider dataProtection)
    {
        _applicationManager = applicationManager;
        _authorizationManager = authorizationManager;
        _scopeManager = scopeManager;
        _serverService = serverService;
        _shellSettings = shellSettings;
        _antiforgery = antiforgery;
        _dataProtection = dataProtection;
    }

    /// <summary>
    /// The authorization endpoint: a GET or a client's POST is the authorization request; a POST
    /// carrying <c>submit.Accept</c> or <c>submit.Deny</c> is the consent page's decision, which
    /// needs a signed-in user and a valid antiforgery token.
    /// </summary>
    public async Task<IResult> AuthorizeEndpointAsync()
    {
        var form = await ReadFormAsync();
        if (form is not null && (form.ContainsKey("submit.Accept") || form.ContainsKey("submit.Deny")))
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Results.Challenge();
            }

            // Warning: unlike the authorization request itself, the decision MUST be validated
            // against antiforgery, as we must be able to reject authorization requests sent by a
            // malicious client that could abuse this interactive endpoint to silently get
            // codes/tokens without the user explicitly approving the authorization demand.
            if (!await IsAntiforgeryValidAsync())
            {
                return AntiforgeryRefused();
            }

            return form.ContainsKey("submit.Accept") ? await AuthorizeAccept() : AuthorizeDeny();
        }

        return await Authorize();
    }

    /// <summary>
    /// The logout endpoint: a GET or a client's POST is the end session request; a POST carrying
    /// <c>submit.Accept</c> or <c>submit.Deny</c> is the confirmation page's decision, which needs
    /// a valid antiforgery token.
    /// </summary>
    public async Task<IResult> LogoutEndpointAsync()
    {
        var form = await ReadFormAsync();
        if (form is not null && (form.ContainsKey("submit.Accept") || form.ContainsKey("submit.Deny")))
        {
            // Warning: unlike the end session request itself, the decision MUST be validated
            // against antiforgery, as we must be able to reject end session requests sent by a
            // malicious client that could abuse this interactive endpoint to silently log the
            // user out without the user explicitly approving the log out operation.
            if (!await IsAntiforgeryValidAsync())
            {
                return AntiforgeryRefused();
            }

            return form.ContainsKey("submit.Accept") ? await LogoutAccept() : LogoutDeny();
        }

        return await Logout();
    }

    private async Task<IFormCollection> ReadFormAsync() =>
        HttpMethods.IsPost(Request.Method) && Request.HasFormContentType ? await Request.ReadFormAsync(HttpContext.RequestAborted) : null;

    private async Task<bool> IsAntiforgeryValidAsync()
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static IResult AntiforgeryRefused() => Results.Problem(
        title: "Bad request",
        detail: "The antiforgery token was missing or invalid.",
        statusCode: StatusCodes.Status400BadRequest);

    private bool ConsumeIgnoreChallengeMarker()
    {
        if (!Request.Cookies.TryGetValue(IgnoreChallengeCookie, out var value) || string.IsNullOrEmpty(value))
        {
            return false;
        }

        Response.Cookies.Delete(IgnoreChallengeCookie, IgnoreChallengeCookieOptions());
        try
        {
            return _dataProtection.CreateProtector(IgnoreChallengePurpose).ToTimeLimitedDataProtector().Unprotect(value) == _shellSettings.Name;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }

    private void SetIgnoreChallengeMarker()
    {
        var value = _dataProtection.CreateProtector(IgnoreChallengePurpose).ToTimeLimitedDataProtector().Protect(_shellSettings.Name, s_ignoreChallengeLifetime);
        var options = IgnoreChallengeCookieOptions();
        options.MaxAge = s_ignoreChallengeLifetime;
        Response.Cookies.Append(IgnoreChallengeCookie, value, options);
    }

    private CookieOptions IgnoreChallengeCookieOptions() => new()
    {
        HttpOnly = true,
        IsEssential = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = (Request.PathBase + Request.Path).Value is { Length: > 0 } path ? path : "/",
    };

    private async Task<IResult> Authorize()
    {
        var response = HttpContext.GetOpenIddictServerResponse();
        if (response is not null)
        {
            return ProtocolError(response.Error, response.ErrorDescription);
        }

        var request = HttpContext.GetOpenIddictServerRequest();
        if (request == null)
        {
            return NotFound();
        }

        // Try to retrieve the user principal stored in the authentication cookie and redirect
        // the user agent to the login page (or to an external provider) in the following cases:
        //
        //  - If the user principal can't be extracted or the cookie is too old.
        //  - If prompt=login was specified by the client application.
        //  - If max_age=0 was specified by the client application (max_age=0 is equivalent to prompt=login).
        //  - If a max_age parameter was provided and the authentication cookie is not considered "fresh" enough.
        var result = await HttpContext.AuthenticateAsync();
        if (result is not { Succeeded: true } ||
            ((request.HasPromptValue(PromptValues.Login) || request.MaxAge is 0 ||
             (request.MaxAge is not null && result.Properties?.IssuedUtc is not null &&
              TimeProvider.System.GetUtcNow() - result.Properties.IssuedUtc > TimeSpan.FromSeconds(request.MaxAge.Value))) &&
            !ConsumeIgnoreChallengeMarker()))
        {
            // If the client application requested promptless authentication,
            // return an error indicating that the user is not logged in.
            if (request.HasPromptValue(PromptValues.None))
            {
                return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is not logged in.",
                    }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            // To avoid endless login endpoint -> authorization endpoint redirects, a short-lived marker cookie is
            // used to skip the challenge if the user agent has already been redirected to the login endpoint.
            //
            // Note: this flag doesn't guarantee that the user has accepted to re-authenticate. If such a guarantee
            // is needed, the existing authentication cookie MUST be deleted AND revoked (e.g using ASP.NET Core
            // Identity's security stamp feature with an extremely short revalidation time span) before triggering
            // a challenge to redirect the user agent to the login endpoint.
            SetIgnoreChallengeMarker();

            return Results.Challenge(new AuthenticationProperties
            {
                RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                    Request.HasFormContentType ? Request.Form : Request.Query),
            });
        }

        var application = await _applicationManager.FindByClientIdAsync(request.ClientId) ??
            throw new InvalidOperationException("The application details cannot be found.");

        var authorizations = await _authorizationManager.FindAsync(
            subject: result.Principal.GetUserIdentifier(),
            client: await _applicationManager.GetIdAsync(application),
            status: Statuses.Valid,
            type: AuthorizationTypes.Permanent,
            scopes: request.GetScopes()).ToListAsync();

        switch (await _applicationManager.GetConsentTypeAsync(application))
        {
            case ConsentTypes.External when authorizations.Count == 0:
                return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.ConsentRequired,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The logged in user is not allowed to access this client application.",
                }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

            case ConsentTypes.Implicit:
            case ConsentTypes.External when authorizations.Count > 0:
            case ConsentTypes.Explicit when authorizations.Count > 0 && !request.HasPromptValue(PromptValues.Consent):
                var identity = new ClaimsIdentity(result.Principal.Claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                identity.AddClaim(new Claim(OpenIdConstants.Claims.EntityType, OpenIdConstants.EntityTypes.User));

                PopulateIdentityClaims(result.Principal, identity);

                identity.SetScopes(request.GetScopes());
                identity.SetResources(await GetResourcesAsync(request.GetScopes()));

                // Automatically create a permanent authorization to avoid requiring explicit consent
                // for future authorization or token requests containing the same scopes.
                var authorization = authorizations.LastOrDefault();
                authorization ??= await _authorizationManager.CreateAsync(
                    identity: identity,
                    subject: identity.GetUserIdentifier(),
                    client: await _applicationManager.GetIdAsync(application),
                    type: AuthorizationTypes.Permanent,
                    scopes: identity.GetScopes());

                identity.SetAuthorizationId(await _authorizationManager.GetIdAsync(authorization));
                identity.SetDestinations(GetDestinations);

                return Results.SignIn(new ClaimsPrincipal(identity), null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            case ConsentTypes.Explicit when request.HasPromptValue(PromptValues.None):
                return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.ConsentRequired,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "Interactive user consent is required.",
                }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

            default:
                // The consent page is the site shell's. It posts the same parameters back here
                // with submit.Accept or submit.Deny and an antiforgery token, which the decision requires.
                return Results.LocalRedirect("~" + ConsentPage + RequestParameters());
        }
    }

    private static void PopulateIdentityClaims(ClaimsPrincipal principal, ClaimsIdentity identity)
    {
        // Note: while ASP.NET Core Identity uses the legacy WS-Federation claims (exposed by the ClaimTypes class),
        // OpenIddict uses the newer JWT claims defined by the OpenID Connect specification. To ensure the mandatory
        // subject claim is correctly populated (and avoid an InvalidOperationException), it's manually added here.
        if (!principal.HasClaim(static claim => claim.Type is Claims.Subject))
        {
            identity.AddClaim(new Claim(Claims.Subject, principal.GetUserIdentifier()));
        }

        if (!principal.HasClaim(static claim => claim.Type is Claims.Name))
        {
            identity.AddClaim(new Claim(Claims.Name, principal.GetUserName()));
        }

        if (!principal.HasClaim(static claim => claim.Type is Claims.Role))
        {
            foreach (var role in principal.GetRoles())
            {
                identity.AddClaim(new Claim(Claims.Role, role));
            }
        }
    }

    private async Task<IResult> AuthorizeAccept()
    {
        var response = HttpContext.GetOpenIddictServerResponse();
        if (response is not null)
        {
            return ProtocolError(response.Error, response.ErrorDescription);
        }

        var request = HttpContext.GetOpenIddictServerRequest();
        if (request == null)
        {
            return NotFound();
        }

        var application = await _applicationManager.FindByClientIdAsync(request.ClientId)
            ?? throw new InvalidOperationException("The application details cannot be found.");

        var authorizations = await _authorizationManager.FindAsync(
            subject: User.GetUserIdentifier(),
            client: await _applicationManager.GetIdAsync(application),
            status: Statuses.Valid,
            type: AuthorizationTypes.Permanent,
            scopes: request.GetScopes()).ToListAsync();

        // Note: the same check is already made in the GET action but is repeated
        // here to ensure a malicious user can't abuse this POST endpoint and
        // force it to return a valid response without the external authorization.
        switch (await _applicationManager.GetConsentTypeAsync(application))
        {
            case ConsentTypes.External when authorizations.Count == 0:
                return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.ConsentRequired,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The logged in user is not allowed to access this client application.",
                }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

            default:
                var identity = new ClaimsIdentity(User.Claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                identity.AddClaim(new Claim(OpenIdConstants.Claims.EntityType, OpenIdConstants.EntityTypes.User));

                PopulateIdentityClaims(User, identity);

                identity.SetScopes(request.GetScopes());
                identity.SetResources(await GetResourcesAsync(request.GetScopes()));

                // Automatically create a permanent authorization to avoid requiring explicit consent
                // for future authorization or token requests containing the same scopes.
                var authorization = authorizations.LastOrDefault();
                authorization ??= await _authorizationManager.CreateAsync(
                    identity: identity,
                    subject: identity.GetUserIdentifier(),
                    client: await _applicationManager.GetIdAsync(application),
                    type: AuthorizationTypes.Permanent,
                    scopes: identity.GetScopes());

                identity.SetAuthorizationId(await _authorizationManager.GetIdAsync(authorization));
                identity.SetDestinations(GetDestinations);

                return Results.SignIn(new ClaimsPrincipal(identity), null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
    }

    private IResult AuthorizeDeny()
    {
        var response = HttpContext.GetOpenIddictServerResponse();
        if (response is not null)
        {
            return ProtocolError(response.Error, response.ErrorDescription);
        }

        var request = HttpContext.GetOpenIddictServerRequest();
        if (request == null)
        {
            return NotFound();
        }

        return Results.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private async Task<IResult> Logout()
    {
        var response = HttpContext.GetOpenIddictServerResponse();
        if (response is not null)
        {
            return ProtocolError(response.Error, response.ErrorDescription);
        }

        var request = HttpContext.GetOpenIddictServerRequest();
        if (request == null)
        {
            return NotFound();
        }

        // Authenticate the current cookie session once and reuse the result for all branches.
        var result = await HttpContext.AuthenticateAsync();

        // If the user is not logged in, allow redirecting the user agent back without
        // rendering a confirmation form.
        if (result == null || !result.Succeeded)
        {
            return Results.SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        // If the server is configured to allow skipping the confirmation prompt and a valid
        // id_token_hint matching the current authenticated user is supplied, sign the user out
        // immediately. The id_token_hint validation (signature, issuer, audience, lifetime) is
        // performed by OpenIddict before the principal is exposed via AuthenticateAsync.
        var settings = await _serverService.GetSettingsAsync();
        if (!settings.RequireEndSessionConfirmation && !string.IsNullOrEmpty(request.IdTokenHint))
        {
            var hintResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (hintResult is { Succeeded: true, Principal: not null })
            {
                var hintSub = hintResult.Principal.GetClaim(Claims.Subject);
                var cookieSub = result.Principal.GetUserIdentifier();

                if (hintSub != null && CryptographicOperations.FixedTimeEquals(
                    MemoryMarshal.AsBytes<char>(hintSub.AsSpan()),
                    MemoryMarshal.AsBytes<char>((cookieSub ?? string.Empty).AsSpan())))
                {
                    return Results.SignOut(
                        new AuthenticationProperties { RedirectUri = "/" },
                        [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
                }
            }
        }

        // The confirmation page is the site shell's.
        return Results.LocalRedirect("~" + LogoutConfirmationPage + RequestParameters());
    }

    /// <summary>The consent page (Grove.Access.OpenId's site library, OpenIdConsent).</summary>
    public const string ConsentPage = "/connect/consent";

    /// <summary>The logout confirmation page (Grove.Access.OpenId's site library, OpenIdConsent).</summary>
    public const string LogoutConfirmationPage = "/connect/logout-confirm";

    // The request's OpenID parameters (query, or form for a POST request), as a query string the
    // page carries back in its form.
    private string RequestParameters()
    {
        IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> parameters = Request.HasFormContentType ? Request.Form : Request.Query;
        var query = QueryString.Empty;
        foreach (var (key, values) in parameters)
        {
            foreach (var value in values)
            {
                query = query.Add(key, value ?? string.Empty);
            }
        }

        return query.ToUriComponent();
    }

    private async Task<IResult> LogoutAccept()
    {
        var response = HttpContext.GetOpenIddictServerResponse();
        if (response is not null)
        {
            return ProtocolError(response.Error, response.ErrorDescription);
        }

        var request = HttpContext.GetOpenIddictServerRequest();
        if (request == null)
        {
            return NotFound();
        }

        await HttpContext.SignOutAsync();

        // If no post_logout_redirect_uri was specified, redirect the user agent
        // to the root page, that should correspond to the home page in most cases.
        if (string.IsNullOrEmpty(request.PostLogoutRedirectUri))
        {
            return Results.LocalRedirect("~/");
        }

        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private IResult LogoutDeny()
    {
        var response = HttpContext.GetOpenIddictServerResponse();
        if (response is not null)
        {
            return ProtocolError(response.Error, response.ErrorDescription);
        }

        var request = HttpContext.GetOpenIddictServerRequest();
        if (request == null)
        {
            return NotFound();
        }

        return Results.LocalRedirect("~/");
    }

    public Task<IResult> Token()
    {
        // Warning: this endpoint skips the API antiforgery check, which is required for this
        // stateless OAuth2/OIDC token endpoint to work correctly.
        // To prevent effective CSRF/session fixation attacks, this endpoint MUST NOT return
        // an authentication cookie or try to establish an ASP.NET Core user session.

        var request = HttpContext.GetOpenIddictServerRequest();
        if (request == null)
        {
            return Task.FromResult(NotFound());
        }

        if (request.IsPasswordGrantType())
        {
            return ExchangePasswordGrantType(request);
        }

        if (request.IsClientCredentialsGrantType())
        {
            return ExchangeClientCredentialsGrantType(request);
        }

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            return ExchangeAuthorizationCodeOrRefreshTokenGrantType(request);
        }

        throw new NotSupportedException("The specified grant type is not supported.");
    }

    private async Task<IResult> ExchangeClientCredentialsGrantType(OpenIddictRequest request)
    {
        if (request.HasScope(Scopes.OfflineAccess))
        {
            return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidScope,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                    "The 'offline_access' scope is not allowed when using the client credentials grant.",
            }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        // Note: client authentication is always enforced by OpenIddict before this action is invoked.
        var application = await _applicationManager.FindByClientIdAsync(request.ClientId) ??
            throw new InvalidOperationException("The application details cannot be found.");

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name, Claims.Role);

        identity.AddClaim(new Claim(OpenIdConstants.Claims.EntityType, OpenIdConstants.EntityTypes.Application));
        identity.AddClaim(new Claim(Claims.Subject, request.ClientId));

        // The application is the caller: the access machinery identifies a caller by the
        // name identifier, and an application has no user record, so its roles ride the token.
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, request.ClientId)
            .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));

        // Always add a "name" claim for grant_type=client_credentials in both
        // access and identity tokens even if the "name" scope wasn't requested.
        identity.AddClaim(new Claim(Claims.Name, await _applicationManager.GetDisplayNameAsync(application))
            .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));

        // The application's roles, as identity: the token carries no Permission claims. The
        // access gate's caller factory resolves an application caller's permissions from the
        // role store under the permission version (docs/access.md § Machine callers).
        foreach (var role in await _applicationManager.GetRolesAsync(application))
        {
            // Since the claims added in this block have a dynamic name, directly set the destinations
            // here instead of relying on the GetDestination() helper that only works with static claims.
            identity.AddClaim(new Claim(identity.RoleClaimType, role)
                .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));
        }

        identity.SetScopes(request.GetScopes());
        identity.SetResources(await GetResourcesAsync(request.GetScopes()));
        identity.SetDestinations(GetDestinations);

        return Results.SignIn(new ClaimsPrincipal(identity), null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IResult> ExchangePasswordGrantType(OpenIddictRequest request)
    {
        var application = await _applicationManager.FindByClientIdAsync(request.ClientId) ??
            throw new InvalidOperationException("The application details cannot be found.");

        // By design, the password flow requires direct username/password validation, which is performed by
        // the user service. If this service is not registered, prevent the password flow from being used.
        var service = HttpContext.RequestServices.GetService<IUserService>();
        if (service == null)
        {
            return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.UnsupportedGrantType,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                    "The resource owner password credentials grant is not supported.",
            }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        string error = null;
        var user = await service.AuthenticateAsync(request.Username, request.Password, (key, message) => error = message);
        if (user == null)
        {
            return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = error,
            }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var principal = await service.CreatePrincipalAsync(user);

        var authorizations = await _authorizationManager.FindAsync(
            subject: principal.GetUserIdentifier(),
            client: await _applicationManager.GetIdAsync(application),
            status: Statuses.Valid,
            type: AuthorizationTypes.Permanent,
            scopes: request.GetScopes()).ToListAsync();

        // If the application is configured to use external consent,
        // reject the request if no existing authorization can be found.
        switch (await _applicationManager.GetConsentTypeAsync(application))
        {
            case ConsentTypes.External when authorizations.Count == 0:
                return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.ConsentRequired,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The logged in user is not allowed to access this client application.",
                }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var identity = (ClaimsIdentity)principal.Identity;
        identity.AddClaim(new Claim(OpenIdConstants.Claims.EntityType, OpenIdConstants.EntityTypes.User));

        PopulateIdentityClaims(principal, identity);

        identity.SetScopes(request.GetScopes());
        identity.SetResources(await GetResourcesAsync(request.GetScopes()));

        // Automatically create a permanent authorization to avoid requiring explicit consent
        // for future authorization or token requests containing the same scopes.
        var authorization = authorizations.FirstOrDefault();
        authorization ??= await _authorizationManager.CreateAsync(
            identity: identity,
            subject: identity.GetUserIdentifier(),
            client: await _applicationManager.GetIdAsync(application),
            type: AuthorizationTypes.Permanent,
            scopes: identity.GetScopes());

        identity.SetAuthorizationId(await _authorizationManager.GetIdAsync(authorization));
        identity.SetDestinations(GetDestinations);

        return Results.SignIn(principal, null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IResult> ExchangeAuthorizationCodeOrRefreshTokenGrantType(OpenIddictRequest request)
    {
        // Retrieve the claims principal stored in the authorization code/refresh token.
        var info = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme) ??
            throw new InvalidOperationException("The user principal cannot be resolved.");

        if (request.IsRefreshTokenGrantType())
        {
            var type = info.Principal.FindFirst(OpenIdConstants.Claims.EntityType)?.Value;
            if (!string.Equals(type, OpenIdConstants.EntityTypes.User, StringComparison.Ordinal))
            {
                return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.UnauthorizedClient,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The refresh token grant type is not allowed for refresh tokens retrieved using the client credentials flow.",
                }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }
        }

        // By default, re-use the principal stored in the authorization code/refresh token.
        var principal = info.Principal;

        // If the user service is available, try to refresh the principal by retrieving
        // the user object from the database and creating a new claims-based principal.
        var service = HttpContext.RequestServices.GetService<IUserService>();
        if (service is not null)
        {
            var user = await service.GetUserByUniqueIdAsync(principal.GetUserIdentifier());
            if (user is not null)
            {
                principal = await service.CreatePrincipalAsync(user);
                // Copy the granted scopes and resources from the original authorization code/refresh token principal
                principal.SetScopes(info.Principal.GetScopes());
                principal.SetResources(await GetResourcesAsync(info.Principal.GetScopes()));
            }
        }

        var identity = (ClaimsIdentity)principal.Identity;
        identity.AddClaim(new Claim(OpenIdConstants.Claims.EntityType, OpenIdConstants.EntityTypes.User));

        PopulateIdentityClaims(principal, identity);

        identity.SetDestinations(GetDestinations);

        return Results.SignIn(principal, null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        // Note: by default, claims are NOT automatically included in the access and identity tokens.
        // To allow OpenIddict to serialize them, you must attach them a destination, that specifies
        // whether they should be included in access tokens, in identity tokens or in both.

        switch (claim.Type)
        {
            // If the claim already includes destinations (set before this helper is called), flow them as-is.
            case string when claim.GetDestinations() is { IsDefaultOrEmpty: false } destinations:
                return destinations;

            // Never include the security stamp in the access and identity tokens, as it's a secret value.
            case "AspNet.Identity.SecurityStamp":
                return [];

            // Only add the claim to the id_token if the corresponding scope was granted.
            // The other claims will only be added to the access_token.
            case OpenIdConstants.Claims.EntityType:
            case Claims.Name when claim.Subject.HasScope(Scopes.Profile):
            case Claims.Email when claim.Subject.HasScope(Scopes.Email):
            case Claims.Role when claim.Subject.HasScope(Scopes.Roles):
                return new[]
                {
                    Destinations.AccessToken,
                    Destinations.IdentityToken,
                };

            default: return new[] { Destinations.AccessToken };
        }
    }

    private async Task<IEnumerable<string>> GetResourcesAsync(ImmutableArray<string> scopes)
    {
        // Note: the current tenant name is always added as a valid resource/audience,
        // which allows the end user to use the corresponding tokens with the APIs
        // located in the current tenant without having to explicitly register a scope.
        var resources = new List<string>()
        {
            OpenIdConstants.Prefixes.Tenant + _shellSettings.Name,
        };

        await foreach (var resource in _scopeManager.ListResourcesAsync(scopes))
        {
            resources.Add(resource);
        }

        return resources;
    }

    /// <summary>The protocol error page (the site's <c>/connect/error</c>, Grove.Access.OpenId's site library) with the error and its description.</summary>
    private static IResult ProtocolError(string error, string description)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(error))
        {
            query.Add("error=" + Uri.EscapeDataString(error));
        }

        if (!string.IsNullOrEmpty(description))
        {
            query.Add("error_description=" + Uri.EscapeDataString(description));
        }

        return Results.LocalRedirect("~/connect/error" + (query.Count > 0 ? "?" + string.Join('&', query) : string.Empty));
    }
}
