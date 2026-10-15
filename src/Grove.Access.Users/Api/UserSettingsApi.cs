#nullable enable

using Grove.Api;
using Grove.Entities;
using Grove.Environment.Options;
using Grove.Infrastructure.Settings;
using Grove.Access.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Access.Users.Api;

/// <param name="External">The external sign-up rules (applied when external authentication is enabled).</param>
public sealed record RegistrationSettingsModel(bool UsersMustValidateEmail, bool UsersAreModerated, ExternalRegistrationSettingsModel External);

public sealed record ExternalRegistrationSettingsModel(bool DisableNewRegistrations, bool NoPassword, bool NoUsername, bool NoEmail, bool UseScriptToGenerateUsername, string? GenerateUsernameScript);

public sealed record ResetPasswordSettingsModel(bool AllowResetPassword);

public sealed record ChangeEmailSettingsModel(bool AllowChangeEmail);

/// <summary>Settings › Users: registration, password reset and email change (the native pages
/// replacing the userRegistration, userResetPassword and userChangeEmail settings groups).</summary>
public sealed class UserSettingsApi : ApiContributor
{
    public override string Name => "UserSettings";

    public override string FeatureId => UserConstants.Features.Users;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("settings/users");
        routes.MapGet("registration", ApiHandlers.Handle<UserSettingsHandlers>(h => h.GetRegistrationAsync()));
        routes.MapPut("registration", (HttpContext http, [FromBody] RegistrationSettingsModel update) => ApiHandlers.For<UserSettingsHandlers>(http).PutRegistrationAsync(update));
        routes.MapGet("reset-password", ApiHandlers.Handle<UserSettingsHandlers>(h => h.GetResetPasswordAsync()));
        routes.MapPut("reset-password", (HttpContext http, [FromBody] ResetPasswordSettingsModel update) => ApiHandlers.For<UserSettingsHandlers>(http).PutResetPasswordAsync(update));
        routes.MapGet("change-email", ApiHandlers.Handle<UserSettingsHandlers>(h => h.GetChangeEmailAsync()));
        routes.MapPut("change-email", (HttpContext http, [FromBody] ChangeEmailSettingsModel update) => ApiHandlers.For<UserSettingsHandlers>(http).PutChangeEmailAsync(update));
    }
}

public sealed class UserSettingsHandlers(
    IAuthorizationService authorization,
    ISiteService siteService,
    IOptionsUpdateNotifier optionsUpdateNotifier) : ApiHandlers
{
    public async Task<IResult> GetRegistrationAsync()
    {
        if (!await CanManageAsync()) return Forbid();
        var site = await siteService.GetSiteSettingsAsync();
        return Ok(ToModel(site.GetOrCreate<RegistrationSettings>(), site.GetOrCreate<ExternalRegistrationSettings>()));
    }

    public async Task<IResult> PutRegistrationAsync(RegistrationSettingsModel update)
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

    public async Task<IResult> GetResetPasswordAsync()
    {
        if (!await CanManageAsync()) return Forbid();
        var settings = (await siteService.GetSiteSettingsAsync()).GetOrCreate<ResetPasswordSettings>();
        return Ok(new ResetPasswordSettingsModel(settings.AllowResetPassword));
    }

    public async Task<IResult> PutResetPasswordAsync(ResetPasswordSettingsModel update)
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

    public async Task<IResult> GetChangeEmailAsync()
    {
        if (!await CanManageAsync()) return Forbid();
        return Ok(new ChangeEmailSettingsModel((await siteService.GetSiteSettingsAsync()).GetOrCreate<ChangeEmailSettings>().AllowChangeEmail));
    }

    public async Task<IResult> PutChangeEmailAsync(ChangeEmailSettingsModel update)
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
