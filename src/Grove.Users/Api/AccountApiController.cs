#nullable enable

using System.Security.Claims;
using System.Text.Json;
using Grove.Modules;
using Grove.Settings;
using Grove.Users.Events;
using Grove.Users.Handlers;
using Grove.Users.Models;
using Grove.Users.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Grove.Users.Api;

/// <summary>The signed-in user's own account, as the account pages show it.</summary>
public sealed record AccountModel(
    string UserName,
    string? Email,
    bool EmailConfirmed,
    bool HasPassword,
    bool AllowChangeEmail,
    AccountLogin[] Logins,
    AccountProvider[] OtherProviders,
    bool CanRemoveLogin);

public sealed record AccountLogin(string LoginProvider, string ProviderKey, string? ProviderDisplayName);

public sealed record AccountProvider(string Name, string DisplayName);

public sealed record AccountChangePassword(string? CurrentPassword, string? NewPassword, string? ConfirmPassword);

public sealed record AccountChangeEmail(string? Email);

public sealed record AccountConfirmEmail(string? UserId, string? Code);

public sealed record AccountLinkRequest(string? Provider, string? ReturnUrl);

public sealed record AccountFieldError(string Key, string Message);

/// <summary>The outcome of an account action; <see cref="Redirect"/> is where the page goes next, if anywhere.</summary>
public sealed record AccountResult(bool Succeeded, AccountFieldError[] Errors, string? Redirect = null)
{
    public static AccountResult Ok(string? redirect = null) => new(true, [], redirect);

    public static AccountResult Refused(string key, string message) => new(false, [new AccountFieldError(key, message)]);
}

/// <summary>
/// Self-service account management for every shell (step 7: the MVC ChangePassword, ChangeEmail,
/// EmailConfirmation and ExternalLogins screens are retired; the account components in
/// Grove.Components call these): the password, the email and its confirmation, and the external
/// logins linked to the account. Only ever the caller's own account, except that a user manager
/// may resend another user's confirmation.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/grove/account")]
public sealed class AccountApiController(
    UserManager<IUser> userManager,
    SignInManager<IUser> signInManager,
    IUserService userService,
    UserEmailService userEmails,
    ISiteService siteService,
    IAuthorizationService authorization,
    IDataProtectionProvider dataProtection,
    IEnumerable<IUserEventHandler> userEventHandlers,
    IStringLocalizer<AccountApiController> localizer,
    ILogger<AccountApiController> logger) : ControllerBase
{
    private const string LinkTicketPurpose = "Grove.Users.Account.LinkLogin";
    private readonly IStringLocalizer S = localizer;

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<AccountModel>> GetAsync()
    {
        if (await userManager.GetUserAsync(User) is not User user)
        {
            return Unauthorized();
        }

        return Ok(await ReadAsync(user));
    }

    [HttpPost("password")]
    [Authorize]
    public async Task<ActionResult<AccountResult>> ChangePasswordAsync([FromBody] AccountChangePassword request)
    {
        if (await userManager.GetUserAsync(User) is not User user)
        {
            return Unauthorized();
        }

        if (string.IsNullOrEmpty(request.NewPassword))
        {
            return BadRequest(AccountResult.Refused(nameof(request.NewPassword), S["The new password is required."]));
        }

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return BadRequest(AccountResult.Refused(nameof(request.ConfirmPassword), S["The new password and confirmation password do not match."]));
        }

        var errors = new List<AccountFieldError>();
        if (await userService.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword, (key, message) => errors.Add(new(key, message))))
        {
            // The security stamp changed: re-issue the session so this browser stays signed in.
            await signInManager.RefreshSignInAsync(user);
            return Ok(AccountResult.Ok());
        }

        return BadRequest(new AccountResult(false, [.. errors]));
    }

    [HttpPost("email")]
    [Authorize]
    public async Task<ActionResult<AccountResult>> ChangeEmailAsync([FromBody] AccountChangeEmail request)
    {
        if (!(await siteService.GetSettingsAsync<ChangeEmailSettings>()).AllowChangeEmail)
        {
            return NotFound();
        }

        if (await userManager.GetUserAsync(User) is not User user)
        {
            return Unauthorized();
        }

        var email = request.Email?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            return BadRequest(AccountResult.Refused(nameof(request.Email), S["The email is required."]));
        }

        if (string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(AccountResult.Refused(nameof(request.Email), S["This email is already your current one."]));
        }

        if (await userManager.FindByEmailAsync(email) is { } other && !string.Equals(other.UserName, user.UserName, StringComparison.Ordinal))
        {
            return BadRequest(AccountResult.Refused(nameof(request.Email), S["A user with the same email already exists."]));
        }

        var errors = new List<AccountFieldError>();
        if (!await userService.ChangeEmailAsync(user, email, (key, message) => errors.Add(new(key, message))))
        {
            return BadRequest(new AccountResult(false, [.. errors]));
        }

        // A changed address is unconfirmed again where the tenant requires confirmation.
        if ((await siteService.GetSettingsAsync<RegistrationSettings>()).UsersMustValidateEmail && !await userManager.IsEmailConfirmedAsync(user))
        {
            await userEmails.SendEmailConfirmationAsync(user);
        }

        await signInManager.RefreshSignInAsync(user);
        return Ok(AccountResult.Ok());
    }

    /// <summary>Sends the email confirmation again: the caller's own, or (with ManageUsers) another user's.</summary>
    [HttpPost("email/send-confirmation")]
    [Authorize]
    public async Task<ActionResult<AccountResult>> SendConfirmationAsync([FromQuery] string? userId = null)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        userId = string.IsNullOrEmpty(userId) ? currentUserId : userId;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        if (userId != currentUserId && !await authorization.AuthorizeAsync(User, UsersPermissions.ManageUsers))
        {
            return Forbid();
        }

        if (await userManager.FindByIdAsync(userId) is not User user)
        {
            return NotFound();
        }

        return await userEmails.SendEmailConfirmationAsync(user)
            ? Ok(AccountResult.Ok())
            : BadRequest(AccountResult.Refused(string.Empty, S["The confirmation email could not be sent."]));
    }

    /// <summary>Confirms an email address from the link the confirmation email carries.</summary>
    [HttpPost("email/confirm")]
    [AllowAnonymous]
    public async Task<ActionResult<AccountResult>> ConfirmEmailAsync([FromBody] AccountConfirmEmail request)
    {
        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Code)
            || await userManager.FindByIdAsync(request.UserId) is not { } user)
        {
            return BadRequest(AccountResult.Refused(string.Empty, S["This confirmation link is not valid."]));
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Code);
        if (!result.Succeeded)
        {
            return BadRequest(AccountResult.Refused(string.Empty, S["This confirmation link is not valid."]));
        }

        var context = new UserConfirmContext(user) { ConfirmationType = UserConfirmationType.Email };
        await userEventHandlers.InvokeAsync((handler, context) => handler.ConfirmedAsync(context), context, logger);
        return Ok(AccountResult.Ok());
    }

    /// <summary>
    /// Starts linking an external login to the caller's account. A fetch (antiforgery-checked)
    /// returns a short-lived ticket URL the page then navigates to: the challenge itself is a
    /// navigation, and the ticket keeps it from being started by another site.
    /// </summary>
    [HttpPost("logins/link")]
    [Authorize]
    public async Task<ActionResult<AccountResult>> StartLinkAsync([FromBody] AccountLinkRequest request)
    {
        if (await userManager.GetUserAsync(User) is not User user)
        {
            return Unauthorized();
        }

        var schemes = await signInManager.GetExternalAuthenticationSchemesAsync();
        if (string.IsNullOrEmpty(request.Provider) || !schemes.Any(scheme => scheme.Name == request.Provider))
        {
            return BadRequest(AccountResult.Refused(nameof(request.Provider), S["Unknown provider."]));
        }

        var ticket = dataProtection.CreateProtector(LinkTicketPurpose).ToTimeLimitedDataProtector().Protect(
            JsonSerializer.Serialize(new LinkTicket(user.UserId, request.Provider, LocalOrNull(request.ReturnUrl))),
            TimeSpan.FromMinutes(5));
        return Ok(AccountResult.Ok($"{Request.PathBase}/api/grove/account/logins/link-start?ticket={Uri.EscapeDataString(ticket)}"));
    }

    [HttpGet("logins/link-start")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> LinkStartAsync([FromQuery] string ticket)
    {
        LinkTicket? link;
        try
        {
            link = JsonSerializer.Deserialize<LinkTicket>(dataProtection.CreateProtector(LinkTicketPurpose).ToTimeLimitedDataProtector().Unprotect(ticket));
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException or JsonException or FormatException)
        {
            return BadRequest();
        }

        if (link is null || link.UserId != userManager.GetUserId(User))
        {
            return Forbid();
        }

        // A clean external cookie, then the provider; its callback adds the login.
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        var redirect = $"{Request.PathBase}/api/grove/account/logins/link-callback?returnUrl={Uri.EscapeDataString(link.ReturnUrl ?? "~/")}";
        var properties = signInManager.ConfigureExternalAuthenticationProperties(link.Provider, redirect, link.UserId);
        return Challenge(properties, link.Provider);
    }

    [HttpGet("logins/link-callback")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> LinkCallbackAsync([FromQuery] string? returnUrl = null)
    {
        var back = LocalOrNull(returnUrl) ?? "~/";
        if (await userManager.GetUserAsync(User) is not User user)
        {
            return Forbid();
        }

        // GetExternalLoginInfoAsync checks the linking user id the challenge recorded.
        var info = await signInManager.GetExternalLoginInfoAsync(await userManager.GetUserIdAsync(user));
        if (info is null)
        {
            logger.LogError("Unexpected error occurred loading external login info for user '{UserName}'.", user.UserName);
            return LocalRedirect(back);
        }

        var result = await userManager.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));
        if (!result.Succeeded)
        {
            logger.LogError("Unexpected error occurred adding external login info for user '{UserName}'.", user.UserName);
        }

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await signInManager.RefreshSignInAsync(user);
        return LocalRedirect(back);
    }

    /// <summary>Removes a linked external login, never the last way to sign in.</summary>
    [HttpDelete("logins/{provider}")]
    [Authorize]
    public async Task<ActionResult<AccountResult>> RemoveLoginAsync(string provider, [FromQuery] string key)
    {
        if (await userManager.GetUserAsync(User) is not User user)
        {
            return Unauthorized();
        }

        var logins = await userManager.GetLoginsAsync(user);
        if (!await userManager.HasPasswordAsync(user) && logins.Count <= 1)
        {
            return BadRequest(AccountResult.Refused(string.Empty, S["The last way to sign in cannot be removed."]));
        }

        var result = await userManager.RemoveLoginAsync(user, provider, key);
        if (!result.Succeeded)
        {
            return BadRequest(new AccountResult(false, [.. result.Errors.Select(error => new AccountFieldError(string.Empty, error.Description))]));
        }

        foreach (var token in user.UserTokens.Where(token => token.LoginProvider == provider).ToList())
        {
            if (!(await userManager.RemoveAuthenticationTokenAsync(user, provider, token.Name)).Succeeded)
            {
                logger.LogError("Could not remove '{TokenName}' token while unlinking '{LoginProvider}' provider from user '{UserName}'.", token.Name, provider, user.UserName);
            }
        }

        await signInManager.RefreshSignInAsync(user);
        return Ok(AccountResult.Ok());
    }

    private async Task<AccountModel> ReadAsync(User user)
    {
        var logins = await userManager.GetLoginsAsync(user);
        var hasPassword = await userManager.HasPasswordAsync(user);
        var schemes = await signInManager.GetExternalAuthenticationSchemesAsync();
        return new AccountModel(
            user.UserName,
            user.Email,
            await userManager.IsEmailConfirmedAsync(user),
            hasPassword,
            (await siteService.GetSettingsAsync<ChangeEmailSettings>()).AllowChangeEmail,
            [.. logins.Select(login => new AccountLogin(login.LoginProvider, login.ProviderKey, login.ProviderDisplayName))],
            [.. schemes.Where(scheme => logins.All(login => login.LoginProvider != scheme.Name)).Select(scheme => new AccountProvider(scheme.Name, scheme.DisplayName ?? scheme.Name))],
            hasPassword || logins.Count > 1);
    }

    private string? LocalOrNull(string? url) => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? url : null;

    private sealed record LinkTicket(string UserId, string Provider, string? ReturnUrl);
}
