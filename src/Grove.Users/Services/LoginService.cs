using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Grove.Users;
using Grove.Users.Events;
using Grove.Users.Models;
using Grove.Users.Services;
using Grove.Users.Workflows;
using Grove.Workflows;

namespace Grove.Users.Services;

/// <summary>
/// The JSON login flow every Grove login surface shares (the admin login shell, and any
/// module surface with its own sign-in - a member portal, say). It fires the SAME
/// ILoginFormEvent sequence the stock MVC AccountController fires, in the same order,
/// so every policy hung on the standard seam (moderation, email confirmation,
/// audit-trail LoggedIn/LogInFailed recording, host-defined gates) applies to every
/// Grove login identically. The one adaptation: a veto handler returns an MVC
/// IActionResult (a page redirect) that a JSON endpoint cannot execute - it is
/// TRANSLATED instead (redirect target + the handler's TempData "error_*" messages
/// into the refusal payload) and any untranslatable result degrades to a generic
/// refusal. Refusal is always the fail-safe direction.
/// </summary>
public sealed class LoginService(
    IUserService users,
    SignInManager<IUser> signInManager,
    UserManager<IUser> userManager,
    IEnumerable<ILoginFormEvent> loginEvents,
    ITempDataDictionaryFactory tempDataFactory,
    IUrlHelperFactory urlHelperFactory,
    ILogger<LoginService> logger)
{
    /// <summary>Runs the credential + event sequence and, on success, issues the
    /// Identity application cookie. <paramref name="configureSession"/> lets the
    /// caller add AuthenticationProperties items (a surface's own session state)
    /// before the cookie is written. A user with two-factor authentication enabled (and
    /// this browser not remembered) is not signed in: the result says a second step is
    /// required and the two-factor cookie holds who passed the first
    /// (<see cref="CompleteTwoFactorAsync"/> finishes it).</summary>
    public async Task<LoginResult> LoginAsync(
        ActionContext actionContext,
        string userName,
        string password,
        bool rememberMe,
        Action<AuthenticationProperties>? configureSession = null)
    {
        var httpContext = actionContext.HttpContext;
        var errors = new List<string>();
        void ReportError(string key, string message) => errors.Add(message);

        // 1. Pre-credential events (stock: before the password check).
        foreach (var loginEvent in loginEvents)
        {
            await loginEvent.LoggingInAsync(userName, ReportError);
        }

        if (errors.Count > 0)
        {
            await NotifyLoginFailedAsync(userName);
            return LoginResult.Refused(new LoginRefusal(errors, null));
        }

        // 2. Credential + built-in policy checks (lockout, disabled, 2FA-required,
        // local-login-disabled) - unchanged behavior.
        var user = await users.AuthenticateAsync(userName, password, ReportError);
        if (user is null)
        {
            await NotifyLoginFailedAsync(userName);
            return LoginResult.Refused(new LoginRefusal(errors, null));
        }

        // 3. Post-credential veto seam (stock: after CheckPasswordSignInAsync, before
        // the cookie is issued).
        foreach (var loginEvent in loginEvents)
        {
            var veto = await loginEvent.ValidatingLoginAsync(user);
            if (veto is not null)
            {
                var refusal = TranslateVeto(actionContext, veto, errors);
                await NotifyLoginFailedAsync(user);
                return LoginResult.Refused(refusal);
            }
        }

        // 4. The second step, when the account has one: the stock password sign-in writes the
        // two-factor cookie (who passed the first step) and the sign-in completes with a code.
        if (await RequiresTwoFactorAsync(user))
        {
            var twoFactor = await signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
            if (twoFactor.RequiresTwoFactor)
            {
                return LoginResult.TwoFactor(user);
            }

            if (!twoFactor.Succeeded)
            {
                errors.Add(twoFactor.IsLockedOut ? "The user is locked out." : "The specified username/password couple is invalid.");
                await NotifyLoginFailedAsync(user);
                return LoginResult.Refused(new LoginRefusal(errors, null));
            }
        }

        var principal = await users.CreatePrincipalAsync(user);
        var properties = new AuthenticationProperties { IsPersistent = rememberMe };
        configureSession?.Invoke(properties);
        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, properties);

        await NotifyLoggedInAsync(httpContext, user, string.Empty);

        return LoginResult.SignedIn(user, principal);
    }

    /// <summary>
    /// The second step of a sign-in <see cref="LoginAsync"/> left pending: the code of
    /// <paramref name="provider"/> (or, with <paramref name="recoveryCode"/>, one of the account's
    /// recovery codes) signs the user in.
    /// </summary>
    public async Task<LoginResult> CompleteTwoFactorAsync(HttpContext httpContext, string? provider, string? code, bool rememberMe, bool rememberDevice, bool recoveryCode)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return LoginResult.Refused(new LoginRefusal(["Sign in again: the sign-in has expired."], null));
        }

        var token = (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = recoveryCode
            ? await signInManager.TwoFactorRecoveryCodeSignInAsync(token)
            : await signInManager.TwoFactorSignInAsync(provider ?? string.Empty, token, rememberMe, rememberDevice);

        if (result.Succeeded)
        {
            await NotifyLoggedInAsync(httpContext, user, string.Empty);
            return LoginResult.SignedIn(user, httpContext.User);
        }

        if (result.IsLockedOut)
        {
            foreach (var loginEvent in loginEvents)
            {
                await loginEvent.IsLockedOutAsync(user);
            }

            return LoginResult.Refused(new LoginRefusal(["The account is locked out."], null));
        }

        await NotifyLoginFailedAsync(user);
        return LoginResult.Refused(new LoginRefusal([recoveryCode ? "Invalid recovery code entered." : "Invalid verification code."], null));
    }

    /// <summary>Whether signing <paramref name="user"/> in takes a second step on this browser.</summary>
    public async Task<bool> RequiresTwoFactorAsync(IUser user) =>
        userManager.SupportsUserTwoFactor
        && await userManager.GetTwoFactorEnabledAsync(user)
        && (await userManager.GetValidTwoFactorProvidersAsync(user)).Count > 0
        && !await signInManager.IsTwoFactorClientRememberedAsync(user);

    // Post-sign-in notifications (the audit trail records LoggedIn here), then the
    // user.logged-in trigger (same payload and correlation as every sign-in path), when the
    // tenant runs workflows.
    private async Task NotifyLoggedInAsync(HttpContext httpContext, IUser user, string provider)
    {
        foreach (var loginEvent in loginEvents)
        {
            await loginEvent.LoggedInAsync(user);
        }

        var triggers = httpContext.RequestServices.GetService<IWorkflowTriggerPublisher>();
        if (triggers is not null && user is User platformUser)
        {
            await triggers.PublishForUserAsync(UserWorkflowTriggers.LoggedIn, platformUser.UserId, platformUser.UserId, platformUser.UserName, UserWorkflowTriggers.Payload(platformUser, provider));
        }
    }

    private async Task NotifyLoginFailedAsync(string userName)
    {
        foreach (var loginEvent in loginEvents)
        {
            await loginEvent.LoggingInFailedAsync(userName);
        }
    }

    private async Task NotifyLoginFailedAsync(IUser user)
    {
        foreach (var loginEvent in loginEvents)
        {
            await loginEvent.LoggingInFailedAsync(user);
        }
    }

    /// <summary>Turns a veto handler's MVC result into JSON the SPA can act on:
    /// redirect-shaped results yield their target path, and the handler's TempData
    /// "error_*" messages (the stock MVC login convention) become the error list.</summary>
    private LoginRefusal TranslateVeto(ActionContext actionContext, IActionResult veto, List<string> errors)
    {
        var tempData = tempDataFactory.GetTempData(actionContext.HttpContext);
        foreach (var key in tempData.Keys.Where(key => key.StartsWith("error", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            if (tempData[key] is string message && !string.IsNullOrWhiteSpace(message))
            {
                errors.Add(message);
            }
        }

        var url = urlHelperFactory.GetUrlHelper(actionContext);
        var redirect = veto switch
        {
            RedirectResult redirectResult => redirectResult.Url,
            LocalRedirectResult localRedirect => localRedirect.Url,
            RedirectToActionResult toAction => url.Action(toAction.ActionName, toAction.ControllerName, toAction.RouteValues),
            RedirectToRouteResult toRoute => url.RouteUrl(toRoute.RouteName, toRoute.RouteValues),
            _ => null,
        };

        // MVC's "~/path" app-base notation means nothing to the SPA client - resolve
        // it to an absolute path on the tenant.
        if (redirect is not null && redirect.StartsWith("~/", StringComparison.Ordinal))
        {
            redirect = actionContext.HttpContext.Request.PathBase + redirect[1..];
        }

        if (redirect is null && veto is not RedirectResult and not LocalRedirectResult and not RedirectToActionResult and not RedirectToRouteResult)
        {
            logger.LogWarning("A login veto returned an untranslatable {ResultType}; refusing generically.", veto.GetType().Name);
        }

        if (errors.Count == 0)
        {
            errors.Add("Login is not allowed for this account.");
        }

        return new LoginRefusal(errors, redirect);
    }
}

/// <summary>Outcome of <see cref="LoginService.LoginAsync"/>: a signed-in user + principal, a
/// sign-in waiting for its second step, or the refusal payload to return as 401.</summary>
public sealed record LoginResult(IUser? User, ClaimsPrincipal? Principal, LoginRefusal? Refusal, bool RequiresTwoFactor = false)
{
    public bool Succeeded => User is not null && Principal is not null;

    public static LoginResult SignedIn(IUser user, ClaimsPrincipal principal) => new(user, principal, null);

    public static LoginResult TwoFactor(IUser user) => new(user, null, null, RequiresTwoFactor: true);

    public static LoginResult Refused(LoginRefusal refusal) => new(null, null, refusal);
}

/// <summary>The 401 payload for a refused login: the human-readable reasons, and the
/// page the equivalent MVC flow would have redirected to (null when none applies).</summary>
public sealed record LoginRefusal(IReadOnlyList<string> Errors, string? Redirect);
