using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Grove.DisplayManagement.Entities;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Environment.Shell;
using Grove.Facebook.Login.Settings;
using Grove.Facebook.Login.ViewModels;
using Grove.Settings;

namespace Grove.Facebook.Login.Drivers;

public sealed class FacebookLoginSettingsDisplayDriver : SiteDisplayDriver<FacebookLoginSettings>
{
    private readonly IShellReleaseManager _shellReleaseManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FacebookLoginSettingsDisplayDriver(
        IShellReleaseManager shellReleaseManager,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _shellReleaseManager = shellReleaseManager;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }
    protected override string SettingsGroupId
        => FacebookConstants.Features.Login;

    public override async Task<IDisplayResult> EditAsync(ISite site, FacebookLoginSettings settings, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (!await _authorizationService.AuthorizeAsync(user, Permissions.ManageFacebookApp))
        {
            return null;
        }

        return Initialize<FacebookLoginSettingsViewModel>("FacebookLoginSettings_Edit", model =>
        {
            model.CallbackPath = settings.CallbackPath.Value;
            model.SaveTokens = settings.SaveTokens;
        }).Location("Content:5")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, FacebookLoginSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (!await _authorizationService.AuthorizeAsync(user, Permissions.ManageFacebookApp))
        {
            return null;
        }

        var model = new FacebookLoginSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.CallbackPath = model.CallbackPath;
        settings.SaveTokens = model.SaveTokens;

        _shellReleaseManager.RequestRelease();

        return await EditAsync(site, settings, context);
    }
}
