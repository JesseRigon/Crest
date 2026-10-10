using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Crest.DisplayManagement.Entities;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Google.TagManager.Settings;
using Crest.Google.TagManager.ViewModels;
using Crest.Settings;

namespace Crest.Google.TagManager.Drivers;

public sealed class GoogleTagManagerSettingsDisplayDriver : SiteDisplayDriver<GoogleTagManagerSettings>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GoogleTagManagerSettingsDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override string SettingsGroupId
        => GoogleConstants.Features.GoogleTagManager;

    public override async Task<IDisplayResult> EditAsync(ISite site, GoogleTagManagerSettings settings, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (!await _authorizationService.AuthorizeAsync(user, Permissions.ManageGoogleTagManager))
        {
            return null;
        }

        return Initialize<GoogleTagManagerSettingsViewModel>("GoogleTagManagerSettings_Edit", model =>
        {
            model.ContainerID = settings.ContainerID;
        }).Location("Content:5")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, GoogleTagManagerSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (!await _authorizationService.AuthorizeAsync(user, Permissions.ManageGoogleTagManager))
        {
            return null;
        }

        var model = new GoogleTagManagerSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.Updater.ModelState.IsValid)
        {
            settings.ContainerID = model.ContainerID;
        }

        return await EditAsync(site, settings, context);
    }
}
