#nullable enable

using Grove.Api;
using Grove.Modules;
using Grove.Security.RateLimits;
using Grove.Infrastructure.Settings;
using Grove.Access.Users.Events;
using Grove.Access.Users.Models;
using Grove.Access.Users.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Access.Users.Api;

public sealed record RegistrationRequest(string? UserName, string? Email, string? Password, string? ConfirmPassword, string? ReturnUrl);

public sealed record ForgotPasswordRequest(string? UserNameOrEmail);

public sealed record ResetPasswordRequest(string? UserNameOrEmail, string? Code, string? NewPassword, string? ConfirmPassword);

/// <summary>
/// Self-registration on the tenant (the site's registration page): the tenant's registration
/// settings, moderation and email confirmation apply, as do the registration form events (a
/// captcha). A registered user is signed in unless a login event refuses, whose page is answered
/// as the redirect (registration pending, confirm your email).
/// </summary>
public sealed class RegistrationApi : ApiContributor
{
    public override string Name => "Registration";

    public override string FeatureId => UserConstants.Features.UserRegistration;

    public override void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("registration", (HttpContext __http, [FromBody] RegistrationRequest request) => ApiHandlers.For<AccountFormsHandlers>(__http).RegisterAsync(request))
            .AllowAnonymous()
            .WithName(RateLimitRouteNames.Register)
            .WithRateLimitGroup(UserRateLimiterPolicyNames.UserRegistration);
    }
}

/// <summary>
/// Password recovery on the tenant (the site's forgot and reset password pages): the tenant's
/// <see cref="ResetPasswordSettings"/> apply, the email links to the reset page, and the answer
/// never says whether the account exists.
/// </summary>
public sealed class PasswordResetApi : ApiContributor
{
    public override string Name => "PasswordReset";

    public override string FeatureId => UserConstants.Features.ResetPassword;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("password");
        routes.MapPost("forgot", (HttpContext __http, [FromBody] ForgotPasswordRequest request) => ApiHandlers.For<AccountFormsHandlers>(__http).ForgotPasswordAsync(request))
            .AllowAnonymous()
            .WithName(RateLimitRouteNames.ForgotPassword)
            .WithRateLimitGroup(UserRateLimiterPolicyNames.PasswordRecovery);
        routes.MapPost("reset", (HttpContext __http, [FromBody] ResetPasswordRequest request) => ApiHandlers.For<AccountFormsHandlers>(__http).ResetPasswordAsync(request))
            .AllowAnonymous()
            .WithName(RateLimitRouteNames.ResetPassword)
            .WithRateLimitGroup(UserRateLimiterPolicyNames.PasswordRecovery);
    }
}

public sealed class AccountFormsHandlers(
    IUserService users,
    UserManager<IUser> userManager,
    ISiteService siteService,
    UserEmailService userEmails,
    IOptions<IdentityOptions> identityOptions,
    IOptionsMonitor<RegistrationOptions> registrationOptions,
    IEnumerable<ILoginFormEvent> loginFormEvents,
    IEnumerable<IPasswordRecoveryFormEvents> recoveryEvents,
    IStringLocalizer<AccountFormsHandlers> localizer,
    ILogger<AccountFormsHandlers> logger) : ApiHandlers
{
    private readonly IStringLocalizer S = localizer;

    public async Task<IResult> RegisterAsync(RegistrationRequest request)
    {
        var errors = new List<AccountFieldError>();
        void Report(string key, string message) => errors.Add(new AccountFieldError(key, message));

        if (!string.IsNullOrEmpty(request.UserName) && await userManager.FindByNameAsync(request.UserName) is not null)
        {
            Report("userName", S["A user with the same username already exists."]);
        }
        else if (identityOptions.Value.User.RequireUniqueEmail && !string.IsNullOrEmpty(request.Email) && await userManager.FindByEmailAsync(request.Email) is not null)
        {
            Report("email", S["A user with the same email address already exists."]);
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            Report("confirmPassword", S["The password and confirmation password do not match."]);
        }

        if (errors.Count > 0)
        {
            return BadRequest(new AccountResult(false, [.. errors]));
        }

        var registered = await users.RegisterAsync(new RegisterUserForm
        {
            UserName = request.UserName,
            Email = request.Email,
            Password = request.Password,
        }, Report);

        if (registered is not User user || errors.Count > 0)
        {
            return BadRequest(new AccountResult(false, errors.Count > 0 ? [.. errors] : [new AccountFieldError(string.Empty, S["The account could not be created."])]));
        }

        // A login event that refuses the new account (moderation, an unconfirmed email) names
        // the page to show instead.
        foreach (var loginFormEvent in loginFormEvents)
        {
            if (await loginFormEvent.ValidatingLoginAsync(user) is { } refusal)
            {
                return Ok(AccountResult.Ok(LoginService.ResolveRedirect(Request, refusal.Redirect) ?? Request.PathBase + "/"));
            }
        }

        return Ok(AccountResult.Ok(IsLocal(request.ReturnUrl) ? request.ReturnUrl : Request.PathBase + "/"));
    }

    public async Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        if (!(await siteService.GetSettingsAsync<ResetPasswordSettings>()).AllowResetPassword)
        {
            return NotFound();
        }

        var errors = new List<AccountFieldError>();
        await recoveryEvents.InvokeAsync((handler, report) => handler.RecoveringPasswordAsync(report), (Action<string, string>)((key, message) => errors.Add(new AccountFieldError(key, message))), logger);
        if (errors.Count > 0)
        {
            return BadRequest(new AccountResult(false, [.. errors]));
        }

        // The same answer whether or not the account exists (or may reset its password yet).
        if (!string.IsNullOrWhiteSpace(request.UserNameOrEmail)
            && await users.GetForgotPasswordUserAsync(request.UserNameOrEmail) is User user
            && (!registrationOptions.CurrentValue.UsersMustValidateEmail || await userManager.IsEmailConfirmedAsync(user)))
        {
            await userEmails.SendPasswordResetAsync(user);
            var context = new PasswordRecoveryContext(user);
            await recoveryEvents.InvokeAsync((handler, context) => handler.PasswordRecoveredAsync(context), context, logger);
        }

        return Ok(AccountResult.Ok(Request.PathBase + AccountPages.ForgotPasswordConfirmation));
    }

    public async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (!(await siteService.GetSettingsAsync<ResetPasswordSettings>()).AllowResetPassword)
        {
            return NotFound();
        }

        var errors = new List<AccountFieldError>();
        void Report(string key, string message) => errors.Add(new AccountFieldError(key, message));

        await recoveryEvents.InvokeAsync((handler, report) => handler.ResettingPasswordAsync(report), (Action<string, string>)Report, logger);
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            Report("confirmPassword", S["The new password and confirmation password do not match."]);
        }

        string token;
        try
        {
            token = Base64.FromUTF8Base64String(request.Code ?? string.Empty);
        }
        catch (FormatException)
        {
            Report("code", S["The reset link is not valid."]);
            token = string.Empty;
        }

        if (errors.Count == 0 && await users.ResetPasswordAsync(request.UserNameOrEmail, token, request.NewPassword, Report))
        {
            if (await users.GetUserAsync(request.UserNameOrEmail) is { } reset)
            {
                var context = new PasswordRecoveryContext(reset);
                await recoveryEvents.InvokeAsync((handler, context) => handler.PasswordResetAsync(context), context, logger);
            }

            return Ok(AccountResult.Ok(Request.PathBase + AccountPages.ResetPasswordConfirmation));
        }

        return BadRequest(new AccountResult(false, errors.Count > 0 ? [.. errors] : [new AccountFieldError(string.Empty, S["The password could not be reset."])]));
    }

    private static bool IsLocal(string? url) =>
        !string.IsNullOrEmpty(url) && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
}
