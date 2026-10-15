using Grove.Entities;
using Grove.Environment.Options;
using Grove.Settings;
using Grove.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grove.Users.Api;

/// <param name="External">The external sign-up rules (applied when external authentication is enabled).</param>
public sealed record RegistrationSettingsModel(bool UsersMustValidateEmail, bool UsersAreModerated, ExternalRegistrationSettingsModel External);

public sealed record ExternalRegistrationSettingsModel(bool DisableNewRegistrations, bool NoPassword, bool NoUsername, bool NoEmail, bool UseScriptToGenerateUsername, string? GenerateUsernameScript);

public sealed record ResetPasswordSettingsModel(bool AllowResetPassword);

public sealed record ChangeEmailSettingsModel(bool AllowChangeEmail);

/// <summary>Settings › Users: registration, password reset and email change (the native pages
/// replacing the userRegistration, userResetPassword and userChangeEmail settings groups).</summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/grove/settings/users")]
public sealed class UserSettingsApiController(
    IAuthorizationService authorization,
    ISiteService siteService,
    IOptionsUpdateNotifier optionsUpdateNotifier) : ControllerBase
{
    [HttpGet("registration")]
    public async Task<ActionResult<RegistrationSettingsModel>> GetRegistrationAsync()
    {
        if (!await CanManageAsync()) return Forbid();
        var site = await siteService.GetSiteSettingsAsync();
        return Ok(ToModel(site.GetOrCreate<RegistrationSettings>(), site.GetOrCreate<ExternalRegistrationSettings>()));
    }

    [HttpPut("registration")]
    public async Task<ActionResult<RegistrationSettingsModel>> PutRegistrationAsync([FromBody] RegistrationSettingsModel update)
    {
        if (!await CanManageAsync()) return Forbid();
        var site = await siteService.LoadSiteSettingsAsync();
        site.Alter<RegistrationSettings>(settings =>
        {
            settings.UsersMustValidateEmail = update.UsersMustValidateEmail;
            settings.UsersAreModerated = update.UsersAreModerated;
        });
        var external = update.External ?? new ExternalRegistrationSettingsModel(false, false, false, false, false, null);
        site.Alter<ExternalRegistrationSettings>(settings =>
        {
            settings.DisableNewRegistrations = external.DisableNewRegistrations;
            settings.NoPassword = external.NoPassword;
            settings.NoUsername = external.NoUsername;
            settings.NoEmail = external.NoEmail;
            settings.UseScriptToGenerateUsername = external.UseScriptToGenerateUsername;
            settings.GenerateUsernameScript = external.GenerateUsernameScript;
        });
        await siteService.UpdateSiteSettingsAsync(site);
        optionsUpdateNotifier.RequestUpdate<RegistrationOptions>();
        return Ok(ToModel(site.GetOrCreate<RegistrationSettings>(), site.GetOrCreate<ExternalRegistrationSettings>()));
    }

    [HttpGet("reset-password")]
    public async Task<ActionResult<ResetPasswordSettingsModel>> GetResetPasswordAsync()
    {
        if (!await CanManageAsync()) return Forbid();
        var settings = (await siteService.GetSiteSettingsAsync()).GetOrCreate<ResetPasswordSettings>();
        return Ok(new ResetPasswordSettingsModel(settings.AllowResetPassword));
    }

    [HttpPut("reset-password")]
    public async Task<ActionResult<ResetPasswordSettingsModel>> PutResetPasswordAsync([FromBody] ResetPasswordSettingsModel update)
    {
        if (!await CanManageAsync()) return Forbid();
        var site = await siteService.LoadSiteSettingsAsync();
        site.Alter<ResetPasswordSettings>(settings =>
        {
            settings.AllowResetPassword = update.AllowResetPassword;
        });
        await siteService.UpdateSiteSettingsAsync(site);
        return Ok(update);
    }

    [HttpGet("change-email")]
    public async Task<ActionResult<ChangeEmailSettingsModel>> GetChangeEmailAsync()
    {
        if (!await CanManageAsync()) return Forbid();
        return Ok(new ChangeEmailSettingsModel((await siteService.GetSiteSettingsAsync()).GetOrCreate<ChangeEmailSettings>().AllowChangeEmail));
    }

    [HttpPut("change-email")]
    public async Task<ActionResult<ChangeEmailSettingsModel>> PutChangeEmailAsync([FromBody] ChangeEmailSettingsModel update)
    {
        if (!await CanManageAsync()) return Forbid();
        var site = await siteService.LoadSiteSettingsAsync();
        site.Alter<ChangeEmailSettings>(settings => settings.AllowChangeEmail = update.AllowChangeEmail);
        await siteService.UpdateSiteSettingsAsync(site);
        return Ok(update);
    }

    private Task<bool> CanManageAsync() => authorization.AuthorizeAsync(User, UsersPermissions.ManageUsers);

    private static RegistrationSettingsModel ToModel(RegistrationSettings settings, ExternalRegistrationSettings external) => new(
        settings.UsersMustValidateEmail,
        settings.UsersAreModerated,
        new ExternalRegistrationSettingsModel(external.DisableNewRegistrations, external.NoPassword, external.NoUsername, external.NoEmail, external.UseScriptToGenerateUsername, external.GenerateUsernameScript));
}
