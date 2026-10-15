#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Environment.Shell;
using Grove.Entities;
using Grove.Security.Services;
using Grove.Infrastructure.Settings;
using Grove.Access.Users;
using Grove.Access.Users.Models;

namespace Grove.Access.Users.Api;

public sealed class LoginSettingsApi : ApiContributor
{
    public override string Name => "LoginSettings";

    public override string FeatureId => "Grove.Access.Users";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("settings/login");
        routes.MapGet("", ApiHandlers.Handle<LoginSettingsHandlers>(h => h.GetAsync()));
        routes.MapPut("", (HttpContext __http, [FromBody] LoginSettingsModel request) => ApiHandlers.For<LoginSettingsHandlers>(__http).SaveAsync(request));
        routes.MapGet("two-factor", ApiHandlers.Handle<TwoFactorMethodSettingsHandlers>(h => h.GetAsync()));
        routes.MapPut("two-factor", (HttpContext __http, [FromBody] TwoFactorMethodSettings request) => ApiHandlers.For<TwoFactorMethodSettingsHandlers>(__http).SaveAsync(request));
    }
}

public sealed class LoginSettingsHandlers(ISiteService sites, IShellReleaseManager releaseManager, IAuthorizationService authorization) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ManageUsers)) return Forbid();
        return Ok(LoginSettingsModel.From(
            await sites.GetSettingsAsync<LoginSettings>(),
            await sites.GetSettingsAsync<TwoFactorLoginSettings>(),
            await sites.GetSettingsAsync<ExternalLoginSettings>()));
    }

    public async Task<IResult> SaveAsync([FromBody] LoginSettingsModel request)
    {
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ManageUsers)) return Forbid();
        if (request.NumberOfRecoveryCodesToGenerate < 1)
            return BadRequest("The number of recovery codes must be at least one.");

        var site = await sites.LoadSiteSettingsAsync();
        site.Alter<LoginSettings>(settings => Copy(request.ToLoginSettings(), settings));
        site.Alter<TwoFactorLoginSettings>(settings => Copy(request.ToTwoFactorLoginSettings(), settings));
        site.Alter<ExternalLoginSettings>(settings => Copy(request.ToExternalLoginSettings(), settings));
        await sites.UpdateSiteSettingsAsync(site);

        // These settings participate in authentication and must take effect for the next request.
        releaseManager.RequestRelease();
        return Ok(request);
    }

    private static void Copy<T>(T source, T destination)
    {
        foreach (var property in typeof(T).GetProperties().Where(property => property.CanRead && property.CanWrite))
            property.SetValue(destination, property.GetValue(source));
    }
}

/// <summary>
/// The two-factor methods' settings on the login settings page, one section per enabled method
/// (authenticator app, email, SMS); a disabled method's section is null. Replaces the methods'
/// settings drivers on the MVC login settings group.
/// </summary>
public sealed class TwoFactorMethodSettingsHandlers(
    ISiteService sites,
    IShellFeaturesManager features,
    IAuthorizationService authorization,
    Grove.Display.Liquid.ILiquidTemplateManager liquidTemplateManager,
    IServiceProvider services) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ManageUsers)) return Forbid();

        var enabled = await EnabledAsync();
        var site = await sites.GetSiteSettingsAsync();
        var app = site.GetOrCreate<AuthenticatorAppLoginSettings>();
        var email = site.GetOrCreate<EmailAuthenticatorLoginSettings>();
        var sms = site.GetOrCreate<SmsAuthenticatorLoginSettings>();
        var roleSettings = site.GetOrCreate<RoleLoginSettings>();

        return Ok(new TwoFactorMethodSettings(
            enabled.Contains(UserConstants.Features.AuthenticatorApp) ? new AuthenticatorAppSettingsModel(app.UseEmailAsAuthenticatorDisplayName, app.TokenLength) : null,
            enabled.Contains(UserConstants.Features.EmailAuthenticator)
                ? new EmailAuthenticatorSettingsModel(
                    string.IsNullOrWhiteSpace(email.Subject) ? EmailAuthenticatorLoginSettings.DefaultSubject : email.Subject,
                    string.IsNullOrWhiteSpace(email.Body) ? EmailAuthenticatorLoginSettings.DefaultBody : email.Body)
                : null,
            enabled.Contains(UserConstants.Features.SmsAuthenticator)
                ? new SmsAuthenticatorSettingsModel(string.IsNullOrWhiteSpace(sms.Body) ? EmailAuthenticatorLoginSettings.DefaultBody : sms.Body)
                : null,
            await AssignableRolesAsync(enabled) is { } roles
                ? new RoleTwoFactorSettingsModel(roleSettings.RequireTwoFactorAuthenticationForSpecificRoles, roleSettings.Roles ?? [], roles)
                : null));
    }

    public async Task<IResult> SaveAsync([FromBody] TwoFactorMethodSettings request)
    {
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ManageUsers)) return Forbid();

        var enabled = await EnabledAsync();
        var errors = new Grove.Validation.ValidationErrors();
        if (request.AuthenticatorApp is { } app && app.TokenLength is not (6 or 8))
        {
            errors.Add("authenticatorApp.tokenLength", "The token length should be either 6 or 8.");
        }

        if (request.Email is { } email)
        {
            if (!liquidTemplateManager.Validate(email.Subject, out var subjectErrors)) errors.Add("email.subject", string.Join(' ', subjectErrors));
            if (!liquidTemplateManager.Validate(email.Body, out var bodyErrors)) errors.Add("email.body", string.Join(' ', bodyErrors));
        }

        if (request.Sms is { } sms && !liquidTemplateManager.Validate(sms.Body, out var smsErrors))
        {
            errors.Add("sms.body", string.Join(' ', smsErrors));
        }

        string[]? selectedRoles = null;
        if (request.Roles is { RequireTwoFactorAuthenticationForSpecificRoles: true } roleRequest && await AssignableRolesAsync(enabled) is { } assignable)
        {
            // Only assignable roles count; at least one is required.
            selectedRoles = [.. assignable.Where(role => (roleRequest.Roles ?? []).Contains(role, StringComparer.OrdinalIgnoreCase))];
            if (selectedRoles.Length == 0)
            {
                errors.Add("roles.roles", "Select at least one role.");
            }
        }

        if (!errors.IsEmpty) return BadRequest(errors);

        var site = await sites.LoadSiteSettingsAsync();
        if (request.AuthenticatorApp is { } appSettings && enabled.Contains(UserConstants.Features.AuthenticatorApp))
        {
            site.Alter<AuthenticatorAppLoginSettings>(settings =>
            {
                settings.UseEmailAsAuthenticatorDisplayName = appSettings.UseEmailAsAuthenticatorDisplayName;
                settings.TokenLength = appSettings.TokenLength;
            });
        }

        if (request.Email is { } emailSettings && enabled.Contains(UserConstants.Features.EmailAuthenticator))
        {
            site.Alter<EmailAuthenticatorLoginSettings>(settings =>
            {
                settings.Subject = emailSettings.Subject;
                settings.Body = emailSettings.Body;
            });
        }

        if (request.Sms is { } smsSettings && enabled.Contains(UserConstants.Features.SmsAuthenticator))
        {
            site.Alter<SmsAuthenticatorLoginSettings>(settings => settings.Body = smsSettings.Body);
        }

        if (request.Roles is { } roleSettings && await AssignableRolesAsync(enabled) is not null)
        {
            site.Alter<RoleLoginSettings>(settings =>
            {
                settings.RequireTwoFactorAuthenticationForSpecificRoles = roleSettings.RequireTwoFactorAuthenticationForSpecificRoles && selectedRoles is { Length: > 0 };
                if (selectedRoles is { Length: > 0 })
                {
                    settings.Roles = selectedRoles;
                }
            });
        }

        await sites.UpdateSiteSettingsAsync(site);
        return await GetAsync();
    }

    /// <summary>The roles two-factor authentication can be required for (Grove.Access.Roles enabled, two-factor on); null otherwise.</summary>
    private async Task<string[]?> AssignableRolesAsync(HashSet<string> enabled)
    {
        if (!enabled.Contains(UserConstants.Features.TwoFactorAuthentication)
            || !enabled.Contains("Grove.Access.Roles")
            || services.GetService(typeof(Grove.Security.Services.IRoleService)) is not Grove.Security.Services.IRoleService roleService)
        {
            return null;
        }

        return [.. (await roleService.GetAssignableRolesAsync()).Select(role => role.RoleName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];
    }

    private async Task<HashSet<string>> EnabledAsync() =>
        (await features.GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal);
}

/// <summary>The enabled two-factor methods' settings; null for a disabled method (or one not sent).</summary>
public sealed record TwoFactorMethodSettings(AuthenticatorAppSettingsModel? AuthenticatorApp, EmailAuthenticatorSettingsModel? Email, SmsAuthenticatorSettingsModel? Sms, RoleTwoFactorSettingsModel? Roles = null);

/// <summary>Two-factor authentication required for the selected roles; <paramref name="AvailableRoles"/> are the assignable roles (ignored on save).</summary>
public sealed record RoleTwoFactorSettingsModel(bool RequireTwoFactorAuthenticationForSpecificRoles, string[]? Roles, string[]? AvailableRoles);

public sealed record AuthenticatorAppSettingsModel(bool UseEmailAsAuthenticatorDisplayName, int TokenLength);

public sealed record EmailAuthenticatorSettingsModel(string? Subject, string? Body);

public sealed record SmsAuthenticatorSettingsModel(string? Body);
