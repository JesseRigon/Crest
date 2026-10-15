using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Grove.Access.Users;
using Grove.Access.Users.Events;
using Grove.Access.Users.Models;
using Grove.Access.Users.Services;
using Grove.Access.Users.Workflows;
using Grove.Workflows;

namespace Grove.Access.Users.Services;

/// <summary>
/// The JSON login flow every Grove login surface shares (the admin login shell, and any
/// module surface with its own sign-in - a member portal, say). It fires the SAME
/// ILoginFormEvent sequence the stock MVC AccountController fires, in the same order,
/// so every policy hung on the standard seam (moderation, email confirmation,
/// audit-trail LoggedIn/LogInFailed recording, host-defined gates) applies to every
/// Grove login identically. A veto handler answers a <see cref="LoginRefusal"/> (its
/// messages and the page to show instead), which becomes the refusal payload, its
/// tenant-relative redirect resolved against the request's path base; a refusal without
/// a message gets a generic one. Refusal is always the fail-safe direction.
/// </summary>
public sealed class LoginService(
    IUserService users,
    SignInManager<IUser> signInManager,
    UserManager<IUser> userManager,
    IEnumerable<ILoginFormEvent> loginEvents)
{
    /// <summary>Runs the credential + event sequence and, on success, issues the
    /// Identity application cookie. <paramref name="configureSession"/> lets the
    /// caller add AuthenticationProperties items (a surface's own session state)
    /// before the cookie is written. A user with two-factor authentication enabled (and
    /// this browser not remembered) is not signed in: the result says a second step is
    /// required and the two-factor cookie holds who passed the first
    /// (<see cref="CompleteTwoFactorAsync"/> finishes it).</summary>
    public async Task<LoginResult> LoginAsync(
        HttpContext httpContext,
        string userName,
        string password,
        bool rememberMe,
        Action<AuthenticationProperties>? configureSession = null)
    {
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
                var refusal = Refusal(httpContext.Request, veto, errors);
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

    /// <summary>A veto handler's refusal as the payload a login surface answers: the messages
    /// reported so far and the handler's, and its redirect resolved on the tenant.</summary>
    private static LoginRefusal Refusal(HttpRequest request, LoginRefusal veto, List<string> errors)
    {
        errors.AddRange(veto.Errors.Where(message => !string.IsNullOrWhiteSpace(message)));
        if (errors.Count == 0)
        {
            errors.Add("Login is not allowed for this account.");
        }

        return new LoginRefusal(errors, ResolveRedirect(request, veto.Redirect));
    }

    /// <summary>Resolves a login event's tenant-relative redirect (<c>~/path</c>) against the
    /// request's path base; any other URL is returned as is.</summary>
    public static string? ResolveRedirect(HttpRequest request, string? redirect) =>
        redirect is not null && redirect.StartsWith("~/", StringComparison.Ordinal) ? request.PathBase + redirect[1..] : redirect;
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
