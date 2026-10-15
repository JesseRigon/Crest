#nullable enable

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Grove.Api;
using Grove.Entities;
using Grove.Environment.Shell;
using Grove.Infrastructure.Settings;
using Grove.Communications.Sms;
using Grove.Access.Users.Events;
using Grove.Access.Users.Models;
using Grove.Access.Users.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Grove.Access.Users.Api;

/// <summary>A two-factor method and whether the signed-in user has it set up.</summary>
public sealed record TwoFactorMethodModel(string Provider, bool IsEnabled);

/// <summary>The signed-in user's two-factor authentication, as the two-factor page shows it.</summary>
public sealed record TwoFactorStateModel(
    bool IsEnabled,
    bool CanDisable,
    bool IsRequired,
    bool IsBrowserRemembered,
    int RecoveryCodesLeft,
    string? PreferredProvider,
    TwoFactorMethodModel[] Methods,
    bool EmailConfirmed,
    string? PhoneNumber,
    bool AllowChangingPhoneNumber);

/// <summary>The authenticator app enrollment: the shared key to type and the URI the QR code carries.</summary>
public sealed record AuthenticatorEnrollmentModel(string SharedKey, string AuthenticatorUri);

/// <summary>An action's outcome: its message and, when the action produced them, the new recovery codes (shown once).</summary>
public sealed record TwoFactorActionResult(bool Succeeded, string? Message, string[]? RecoveryCodes = null);

public sealed record TwoFactorCodeRequest(string? Code);

public sealed record TwoFactorProviderRequest(string? Provider);

public sealed record TwoFactorPhoneRequest(string? PhoneNumber, string? Code);

/// <summary>
/// The signed-in user's own two-factor authentication (the two-factor page): turning it on and
/// off, the preferred method, forgetting this browser, recovery codes, and setting up the
/// authenticator app and the phone number for SMS codes. Only ever the caller's own account.
/// </summary>
public sealed class TwoFactorApi : ApiContributor
{
    public override string Name => "TwoFactor";

    public override string FeatureId => UserConstants.Features.TwoFactorAuthentication;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("two-factor");
        routes.MapGet("", ApiHandlers.Handle<TwoFactorHandlers>(h => h.GetAsync()));
        routes.MapPost("enable", ApiHandlers.Handle<TwoFactorHandlers>(h => h.EnableAsync()));
        routes.MapPost("disable", ApiHandlers.Handle<TwoFactorHandlers>(h => h.DisableAsync()));
        routes.MapPost("preferred", (HttpContext __http, [FromBody] TwoFactorProviderRequest request) => ApiHandlers.For<TwoFactorHandlers>(__http).SetPreferredAsync(request));
        routes.MapPost("forget-browser", ApiHandlers.Handle<TwoFactorHandlers>(h => h.ForgetBrowserAsync()));
        routes.MapPost("recovery-codes", ApiHandlers.Handle<TwoFactorHandlers>(h => h.GenerateRecoveryCodesAsync()));
        routes.MapGet("authenticator", ApiHandlers.Handle<TwoFactorHandlers>(h => h.AuthenticatorAsync()));
        routes.MapPost("authenticator", (HttpContext __http, [FromBody] TwoFactorCodeRequest request) => ApiHandlers.For<TwoFactorHandlers>(__http).VerifyAuthenticatorAsync(request));
        routes.MapPost("authenticator/reset", ApiHandlers.Handle<TwoFactorHandlers>(h => h.ResetAuthenticatorAsync()));
        routes.MapPost("phone/send", (HttpContext __http, [FromBody] TwoFactorPhoneRequest request) => ApiHandlers.For<TwoFactorHandlers>(__http).SendPhoneCodeAsync(request));
        routes.MapPost("phone/verify", (HttpContext __http, [FromBody] TwoFactorPhoneRequest request) => ApiHandlers.For<TwoFactorHandlers>(__http).VerifyPhoneAsync(request));
    }
}

public sealed class TwoFactorHandlers(
    UserManager<IUser> userManager,
    SignInManager<IUser> signInManager,
    ISiteService siteService,
    ITwoFactorAuthenticationHandlerCoordinator coordinator,
    IOptions<TwoFactorOptions> twoFactorOptions,
    IOptions<IdentityOptions> identityOptions,
    UrlEncoder urlEncoder,
    ShellSettings shellSettings,
    IServiceProvider services,
    IStringLocalizer<TwoFactorHandlers> localizer) : ApiHandlers
{
    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&digits={3}&issuer={0}";

    private readonly IStringLocalizer S = localizer;

    public async Task<IResult> GetAsync()
    {
        if (await userManager.GetUserAsync(User) is not { } user)
        {
            return Unauthorized();
        }

        var providers = await ProvidersAsync(user);
        var required = await coordinator.IsRequiredAsync(user);
        var phoneNumber = await userManager.GetPhoneNumberAsync(user);
        var loginSettings = await siteService.GetSettingsAsync<LoginSettings>();
        var phoneValid = !string.IsNullOrEmpty(phoneNumber) && services.GetService<IPhoneFormatValidator>()?.IsValid(phoneNumber) != false;

        return Ok(new TwoFactorStateModel(
            await userManager.GetTwoFactorEnabledAsync(user),
            !required,
            required,
            await signInManager.IsTwoFactorClientRememberedAsync(user),
            await userManager.CountRecoveryCodesAsync(user),
            user is User platformUser && platformUser.TryGet<TwoFactorPreference>(out var preference) ? preference.DefaultProvider : null,
            [.. twoFactorOptions.Value.Providers.Select(provider => new TwoFactorMethodModel(provider, providers.Contains(provider)))],
            await userManager.IsEmailConfirmedAsync(user),
            phoneNumber,
            loginSettings.AllowChangingPhoneNumber || !phoneValid));
    }

    public async Task<IResult> EnableAsync()
    {
        if (await userManager.GetUserAsync(User) is not { } user)
        {
            return Unauthorized();
        }

        if ((await ProvidersAsync(user)).Count == 0)
        {
            return BadRequest(new TwoFactorActionResult(false, S["To enable two-factor authentication, enable at least one two-factor method."]));
        }

        await EnableTwoFactorAsync(user);
        return Ok(new TwoFactorActionResult(true, S["Two-factor authentication has been enabled."], await EnsureRecoveryCodesAsync(user)));
    }

    public async Task<IResult> DisableAsync()
    {
        if (await userManager.GetUserAsync(User) is not { } user)
        {
            return Unauthorized();
        }

        if (await coordinator.IsRequiredAsync(user))
        {
            return BadRequest(new TwoFactorActionResult(false, S["Two-factor authentication cannot be disabled for the current user."]));
        }

        var result = await userManager.SetTwoFactorEnabledAsync(user, false);
        return result.Succeeded
            ? Ok(new TwoFactorActionResult(true, S["Two-factor authentication has been disabled."]))
            : BadRequest(new TwoFactorActionResult(false, S["Unexpected error occurred disabling two-factor authentication."]));
    }

    public async Task<IResult> SetPreferredAsync(TwoFactorProviderRequest request)
    {
        if (await userManager.GetUserAsync(User) is not User user)
        {
            return Unauthorized();
        }

        if (string.IsNullOrEmpty(request.Provider) || !(await ProvidersAsync(user)).Contains(request.Provider))
        {
            return BadRequest(new TwoFactorActionResult(false, S["Unable to update preferences."]));
        }

        user.Put(new TwoFactorPreference { DefaultProvider = request.Provider });
        await userManager.UpdateAsync(user);
        return Ok(new TwoFactorActionResult(true, S["Preferences were updated successfully."]));
    }

    public async Task<IResult> ForgetBrowserAsync()
    {
        if (await userManager.GetUserAsync(User) is null)
        {
            return Unauthorized();
        }

        await signInManager.ForgetTwoFactorClientAsync();
        return Ok(new TwoFactorActionResult(true, S["The current browser has been forgotten. When you login again from this browser you will be prompted for your 2fa code."]));
    }

    public async Task<IResult> GenerateRecoveryCodesAsync()
    {
        if (await userManager.GetUserAsync(User) is not { } user)
        {
            return Unauthorized();
        }

        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            return BadRequest(new TwoFactorActionResult(false, S["Cannot generate recovery codes for user because they do not have 2FA enabled."]));
        }

        return Ok(new TwoFactorActionResult(true, S["You have generated new recovery codes."], await NewRecoveryCodesAsync(user)));
    }

    /// <summary>The authenticator app enrollment; the key is created when the user has none.</summary>
    public async Task<IResult> AuthenticatorAsync()
    {
        if (await userManager.GetUserAsync(User) is not { } user)
        {
            return Unauthorized();
        }

        if (!twoFactorOptions.Value.Providers.Contains(TokenOptions.DefaultAuthenticatorProvider))
        {
            return NotFound();
        }

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user) ?? string.Empty;
        }

        var settings = await siteService.GetSettingsAsync<AuthenticatorAppLoginSettings>();
        var site = await siteService.GetSiteSettingsAsync();
        var issuer = string.IsNullOrWhiteSpace(site.SiteName) ? shellSettings.Name : site.SiteName.Trim();
        var displayName = settings.UseEmailAsAuthenticatorDisplayName ? await userManager.GetEmailAsync(user) : await userManager.GetUserNameAsync(user);
        var uri = string.Format(CultureInfo.InvariantCulture, AuthenticatorUriFormat, urlEncoder.Encode(issuer), urlEncoder.Encode(displayName ?? string.Empty), key, settings.TokenLength);

        return Ok(new AuthenticatorEnrollmentModel(FormatKey(key), uri));
    }

    public async Task<IResult> VerifyAuthenticatorAsync(TwoFactorCodeRequest request)
    {
        if (await userManager.GetUserAsync(User) is not { } user)
        {
            return Unauthorized();
        }

        if (!await userManager.VerifyTwoFactorTokenAsync(user, identityOptions.Value.Tokens.AuthenticatorTokenProvider, StripToken(request.Code)))
        {
            return BadRequest(new TwoFactorActionResult(false, S["Verification code is invalid."]));
        }

        await EnableTwoFactorAsync(user);
        return Ok(new TwoFactorActionResult(true, S["Your authenticator app has been verified."], await EnsureRecoveryCodesAsync(user)));
    }

    public async Task<IResult> ResetAuthenticatorAsync()
    {
        if (await userManager.GetUserAsync(User) is not { } user)
        {
            return Unauthorized();
        }

        return await RemoveMethodAsync(user, async () =>
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            return S["Your authenticator app key has been reset."];
        });
    }

    public async Task<IResult> SendPhoneCodeAsync(TwoFactorPhoneRequest request)
    {
        if (await userManager.GetUserAsync(User) is not { } user)
        {
            return Unauthorized();
        }

        var validator = services.GetService<IPhoneFormatValidator>();
        var current = await userManager.GetPhoneNumberAsync(user);
        var settings = await siteService.GetSettingsAsync<LoginSettings>();
        var canChange = settings.AllowChangingPhoneNumber || string.IsNullOrEmpty(current) || validator?.IsValid(current) == false;
        var phoneNumber = canChange ? request.PhoneNumber : current;

        if (string.IsNullOrWhiteSpace(phoneNumber) || validator?.IsValid(phoneNumber) == false)
        {
            return BadRequest(new TwoFactorActionResult(false, S["Invalid phone number used."]));
        }

        return await services.GetRequiredService<TwoFactorCodeSender>().SendPhoneConfirmationAsync(user, phoneNumber)
            ? Ok(new TwoFactorActionResult(true, S["We have successfully sent an verification code to your phone number. Please retrieve the code from your device."]))
            : BadRequest(new TwoFactorActionResult(false, S["We are unable to send you an SMS message at this time. Please try again later."]));
    }

    public async Task<IResult> VerifyPhoneAsync(TwoFactorPhoneRequest request)
    {
        if (await userManager.GetUserAsync(User) is not { } user)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new TwoFactorActionResult(false, S["Unable to find a phone number."]));
        }

        // The code was generated for this number: a different number does not verify.
        var result = await userManager.ChangePhoneNumberAsync(user, request.PhoneNumber, StripToken(request.Code));
        if (!result.Succeeded)
        {
            return BadRequest(new TwoFactorActionResult(false, S["Invalid verification code."]));
        }

        await EnableTwoFactorAsync(user);
        return Ok(new TwoFactorActionResult(true, S["Your phone number has been confirmed."], await EnsureRecoveryCodesAsync(user)));
    }

    // Turns two-factor authentication on (once a method is set up) and, for a user who had to,
    // drops the "set it up first" claim from the session.
    private async Task EnableTwoFactorAsync(IUser user)
    {
        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            await userManager.SetTwoFactorEnabledAsync(user, true);
            if (!await coordinator.IsRequiredAsync(user))
            {
                return;
            }
        }

        if (User.Claims.FirstOrDefault(claim => claim.Type == UserConstants.TwoFactorAuthenticationClaimType) is { } claim)
        {
            await userManager.RemoveClaimAsync(user, claim);
            await signInManager.RefreshSignInAsync(user);
        }
    }

    // Removing the only method turns two-factor authentication off, unless it is required.
    private async Task<IResult> RemoveMethodAsync(IUser user, Func<Task<string>> remove)
    {
        var providers = await ProvidersAsync(user);
        if (providers.Count == 1)
        {
            if (await coordinator.IsRequiredAsync(user))
            {
                return BadRequest(new TwoFactorActionResult(false, S["You cannot remove the only active two-factor method."]));
            }

            if (!(await userManager.SetTwoFactorEnabledAsync(user, false)).Succeeded)
            {
                return BadRequest(new TwoFactorActionResult(false, S["Unable to disable two-factor authentication."]));
            }

            var message = await remove();
            await signInManager.RefreshSignInAsync(user);
            return Ok(new TwoFactorActionResult(true, message + " " + S["Your two-factor authentication has been disabled."]));
        }

        return Ok(new TwoFactorActionResult(true, await remove()));
    }

    // New recovery codes when the user has none left (the first method set up): shown once.
    private async Task<string[]?> EnsureRecoveryCodesAsync(IUser user) =>
        await userManager.CountRecoveryCodesAsync(user) == 0 ? await NewRecoveryCodesAsync(user) : null;

    private async Task<string[]> NewRecoveryCodesAsync(IUser user)
    {
        var settings = await siteService.GetSettingsAsync<TwoFactorLoginSettings>();
        return [.. await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, settings.NumberOfRecoveryCodesToGenerate) ?? []];
    }

    private async Task<IList<string>> ProvidersAsync(IUser user) =>
        [.. (await userManager.GetValidTwoFactorProvidersAsync(user)).Intersect(twoFactorOptions.Value.Providers)];

    private static string StripToken(string? code) => (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);

    private static string FormatKey(string key)
    {
        var result = new StringBuilder();
        for (var position = 0; position < key.Length; position += 4)
        {
            result.Append(key.AsSpan(position, Math.Min(4, key.Length - position))).Append(' ');
        }

        return result.ToString().TrimEnd().ToLowerInvariant();
    }
}
