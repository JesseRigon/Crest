#nullable enable

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Api;
using Grove.Modules;
using Grove.Security.RateLimits;
using Grove.Infrastructure.Settings;
using Grove.Access.Users.Models;
using Grove.Access.Users.Services;
using Grove.Access.Users.Events;
using Grove.Access;
using Grove.Entities;

namespace Grove.Access.Users.Api;

/// <summary>
/// Signing in (docs/access.md: Access declares the sign-in API): who the caller is, the sign-in
/// form's contributions, the password sign-in and its second step, the external sign-in's start,
/// and signing out. Every login surface calls these; the stock login form events apply to each.
/// </summary>
public sealed class AuthApi : ApiContributor
{
    /// <summary>The sign-out endpoint, tenant-relative: the identity cookie's logout path.</summary>
    public const string LogoutPath = "/api/grove/auth/logout";

    public override string Name => "Auth";

    public override string FeatureId => "Grove.Access.Users";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("auth");
        routes.MapGet("me", ApiHandlers.Handle<AuthHandlers>(h => h.Me()));
        routes.MapGet("form/{form}", (HttpContext __http, [FromRoute] string form, [FromQuery] string? returnUrl) => ApiHandlers.For<AuthHandlers>(__http).FormAsync(form, returnUrl))
            .AllowAnonymous();
        routes.MapPost("login", (HttpContext __http, [FromBody] LoginRequest request) => ApiHandlers.For<AuthHandlers>(__http).Login(request))
            .AllowAnonymous()
            .WithName(RateLimitRouteNames.Login)
            .WithRateLimitGroup(UserRateLimiterPolicyNames.PasswordAuthentication);
        routes.MapPost("logout", ApiHandlers.Handle<AuthHandlers>(h => h.Logout()));

        // A navigation, not a fetch: the challenge redirects the browser to the provider.
        routes.MapGet("external-login", (HttpContext __http, [FromQuery] string? provider, [FromQuery] string? returnUrl) => ApiHandlers.For<AuthHandlers>(__http).ExternalLoginAsync(provider, returnUrl))
            .AllowAnonymous();

        // The second step of a sign-in the password step left pending (the two-factor cookie).
        routes.MapGet("two-factor", ApiHandlers.Handle<AuthHandlers>(h => h.TwoFactorAsync()))
            .AllowAnonymous();
        routes.MapPost("two-factor", (HttpContext __http, [FromBody] TwoFactorSignInRequest request) => ApiHandlers.For<AuthHandlers>(__http).TwoFactorSignInAsync(request))
            .AllowAnonymous()
            .WithName(RateLimitRouteNames.LoginWithTwoFactorAuthentication)
            .WithRateLimitGroup(UserRateLimiterPolicyNames.TwoFactorAuthentication);
        routes.MapPost("two-factor/recovery", (HttpContext __http, [FromBody] RecoveryCodeSignInRequest request) => ApiHandlers.For<AuthHandlers>(__http).RecoveryCodeSignInAsync(request))
            .AllowAnonymous()
            .WithName(RateLimitRouteNames.LoginWithRecoveryCode)
            .WithRateLimitGroup(UserRateLimiterPolicyNames.TwoFactorRecovery);
        routes.MapPost("two-factor/send-code", (HttpContext __http, [FromBody] TwoFactorSendCodeRequest request) => ApiHandlers.For<AuthHandlers>(__http).SendCodeAsync(request))
            .AllowAnonymous()
            .WithName(RateLimitRouteNames.TwoFactorSendCode)
            .WithRateLimitGroup(UserRateLimiterPolicyNames.TwoFactorCodeSend);
    }
}

public sealed class AuthHandlers(
    LoginService loginService,
    ICallerContextAccessor callers,
    Grove.Access.Users.Services.IUserService userService,
    SignInManager<IUser> signInManager,
    UserManager<IUser> userManager,
    ISiteService siteService,
    IOptions<TwoFactorOptions> twoFactorOptions,
    IEnumerable<ILoginExtensionProvider> extensionProviders,
    IEnumerable<Grove.Access.Users.Events.ILoginFormEvent> loginFormEvents,
    IEnumerable<Grove.Access.Users.Events.ILogoutFormEvent> logoutFormEvents,
    IServiceProvider services,
    Microsoft.Extensions.Logging.ILogger<AuthHandlers> logger) : ApiHandlers
{
    /// <summary>The request's caller as the gate built it: identity and roles, never the claims.</summary>
    public IResult Me()
    {
        if (callers.Current is not { IsAuthenticated: true } caller)
        {
            return Ok(new AuthUser(false, null, []));
        }

        return Ok(new AuthUser(true, caller.UserName, caller.Roles.ToArray()));
    }

    /// <summary>
    /// A sign-in form's shape: whether local sign-in is offered, the remember-me policy, and what
    /// the enabled features contribute (links, provider buttons, sections). On the login form a
    /// login event that sends the browser elsewhere straight away (the only external provider)
    /// answers its address as <see cref="AccessFormModel.Redirect"/>.
    /// </summary>
    public async Task<IResult> FormAsync(string form, string? returnUrl)
    {
        if (!IsLocal(returnUrl))
        {
            returnUrl = null;
        }

        var context = new LoginExtensionContext(form, HttpContext, returnUrl);
        var extensions = new List<LoginExtension>();
        foreach (var provider in extensionProviders)
        {
            extensions.AddRange(await provider.GetExtensionsAsync(context));
        }

        var settings = await siteService.GetSettingsAsync<LoginSettings>();
        string? redirect = null;
        if (form == AccessForms.Login && !Request.Query.ContainsKey("externalLoginError"))
        {
            foreach (var loginEvent in loginFormEvents)
            {
                if (await loginEvent.LoggingInAsync() is { Redirect: { } detour })
                {
                    redirect = LoginService.ResolveRedirect(Request, detour);
                    break;
                }
            }
        }

        return Ok(new AccessFormModel(
            !settings.DisableLocalLogin,
            settings.AllowRememberMe,
            settings.UsePersistentAuthenticationCookie,
            [.. extensions.OrderBy(extension => extension.Kind).ThenBy(extension => extension.Order)],
            redirect));
    }

    // The admin login shell's JSON adapter over the shared LoginService flow (the
    // full stock ILoginFormEvent sequence, a refusal answered as the 401 payload).
    public async Task<IResult> Login(LoginRequest request)
    {
        var settings = await siteService.GetSettingsAsync<LoginSettings>();
        var rememberMe = settings.AllowRememberMe ? request.RememberMe : settings.UsePersistentAuthenticationCookie;
        var result = await loginService.LoginAsync(HttpContext, request.UserName, request.Password, rememberMe);
        if (result.RequiresTwoFactor)
        {
            return Ok(new AuthUser(false, null, [], RequiresTwoFactor: true));
        }

        if (!result.Succeeded)
        {
            return Unauthorized(result.Refusal);
        }

        // The gate built this request's caller before the sign-in; the roles come from the
        // user just signed in, which the next request's caller will carry.
        return Ok(SignedIn(result));
    }

    /// <summary>The pending second step: the methods the account has and the one to ask for first.</summary>
    public async Task<IResult> TwoFactorAsync()
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is not { } user)
        {
            return NotFound();
        }

        var providers = await ProvidersAsync(user);
        var preferred = user is User platformUser && platformUser.TryGet<TwoFactorPreference>(out var preference) && providers.Contains(preference.DefaultProvider)
            ? preference.DefaultProvider
            : providers.FirstOrDefault();
        var settings = await siteService.GetSettingsAsync<TwoFactorLoginSettings>();

        return Ok(new TwoFactorChallengeModel([.. providers], preferred, settings.AllowRememberClientTwoFactorAuthentication));
    }

    public async Task<IResult> TwoFactorSignInAsync(TwoFactorSignInRequest request)
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is { } user && !(await ProvidersAsync(user)).Contains(request.Provider ?? string.Empty))
        {
            return Unauthorized(new LoginRefusal(["Unable to find an active two-factor provider."], null));
        }

        var settings = await siteService.GetSettingsAsync<TwoFactorLoginSettings>();
        var result = await loginService.CompleteTwoFactorAsync(HttpContext, request.Provider, request.Code, request.RememberMe, settings.AllowRememberClientTwoFactorAuthentication && request.RememberDevice, recoveryCode: false);
        return result.Succeeded ? Ok(SignedIn(result)) : Unauthorized(result.Refusal);
    }

    public async Task<IResult> RecoveryCodeSignInAsync(RecoveryCodeSignInRequest request)
    {
        var result = await loginService.CompleteTwoFactorAsync(HttpContext, null, request.Code, false, false, recoveryCode: true);
        return result.Succeeded ? Ok(SignedIn(result)) : Unauthorized(result.Refusal);
    }

    /// <summary>Sends the pending sign-in's code for a method that delivers one (email, SMS).</summary>
    public async Task<IResult> SendCodeAsync(TwoFactorSendCodeRequest request)
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is not { } user || !(await ProvidersAsync(user)).Contains(request.Provider ?? string.Empty))
        {
            return BadRequest(new TwoFactorCodeSent(false, "Sign in again: the sign-in has expired."));
        }

        var (sent, message) = await services.GetRequiredService<TwoFactorCodeSender>().SendSignInCodeAsync(user, request.Provider!);
        return sent ? Ok(new TwoFactorCodeSent(true, message)) : BadRequest(new TwoFactorCodeSent(false, message));
    }

    /// <summary>Starts an external sign-in: the stock challenge, finished by the platform's callback.</summary>
    public async Task<IResult> ExternalLoginAsync(string? provider, string? returnUrl)
    {
        var schemes = await signInManager.GetExternalAuthenticationSchemesAsync();
        if (string.IsNullOrWhiteSpace(provider) || !schemes.Any(scheme => scheme.Name == provider))
        {
            return ApiProblems.BadRequest("Unknown provider.");
        }

        if (!IsLocal(returnUrl))
        {
            returnUrl = null;
        }

        var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, ExternalAuthenticationApi.CallbackUrl(Request, returnUrl));
        return Results.Challenge(properties, [provider]);
    }

    public async Task<IResult> Logout()
    {
        // The same logout events as every sign-out (the audit trail's sign-out record among
        // them, docs/audit.md › step 2): before the sign-out, while the session is still there.
        var user = await userService.GetAuthenticatedUserAsync(User);
        if (user is not null)
        {
            await logoutFormEvents.InvokeAsync((handler, user) => handler.LoggingOutAsync(user), user, logger);
        }

        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        if (user is not null)
        {
            await logoutFormEvents.InvokeAsync((handler, user) => handler.LoggedOutAsync(user), user, logger);
        }

        return Ok(new AuthUser(false, null, []));
    }

    // A user whose roles require two-factor authentication and who has not set it up goes to
    // the two-factor page first.
    private AuthUser SignedIn(LoginResult result) => new(
        true,
        result.User!.UserName,
        (result.User as User)?.RoleNames.ToArray() ?? [],
        Redirect: result.Principal?.HasClaim(claim => claim.Type == UserConstants.TwoFactorAuthenticationClaimType) == true
            ? Request.PathBase + AccountPages.TwoFactor
            : null);

    private async Task<IList<string>> ProvidersAsync(IUser user) =>
        [.. (await userManager.GetValidTwoFactorProvidersAsync(user)).Intersect(twoFactorOptions.Value.Providers)];

    private static bool IsLocal(string? url) =>
        !string.IsNullOrEmpty(url) && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
}
