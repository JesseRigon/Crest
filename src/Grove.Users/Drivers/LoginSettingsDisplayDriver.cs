using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Grove.DisplayManagement.Entities;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Settings;
using Grove.Users.Models;

namespace Grove.Users.Drivers;

public sealed class LoginSettingsDisplayDriver : SiteDisplayDriver<LoginSettings>
{
    public const string GroupId = "userLogin";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public LoginSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override IDisplayResult Edit(ISite site, LoginSettings settings, BuildEditorContext context)
    {
        return Initialize<LoginSettings>("LoginSettings_Edit", model =>
        {
            model.UseSiteTheme = settings.UseSiteTheme;
            model.DisableLocalLogin = settings.DisableLocalLogin;
            model.AllowChangingEmail = settings.AllowChangingEmail;
            model.AllowChangingUsername = settings.AllowChangingUsername;
            model.AllowChangingPhoneNumber = settings.AllowChangingPhoneNumber;
            model.AllowRememberMe = settings.AllowRememberMe;
            model.UsePersistentAuthenticationCookie = settings.UsePersistentAuthenticationCookie;
        }).Location("Content:5#General")
        .RenderWhen(static (driver) => driver._authorizationService.AuthorizeAsync(driver._httpContextAccessor.HttpContext.User, UsersPermissions.ManageUsers), this)
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, LoginSettings section, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, UsersPermissions.ManageUsers))
        {
            return null;
        }

        await context.Updater.TryUpdateModelAsync(section, Prefix);

        return await EditAsync(site, section, context);
    }
}
