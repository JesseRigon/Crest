using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Crest.ContentLocalization.Models;
using Crest.DisplayManagement.Entities;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Settings;

namespace Crest.ContentLocalization.Drivers;

public sealed class ContentCulturePickerSettingsDriver : SiteDisplayDriver<ContentCulturePickerSettings>
{
    public const string GroupId = "ContentCulturePicker";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public ContentCulturePickerSettingsDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, ContentCulturePickerSettings settings, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, ContentLocalizationPermissions.ManageContentCulturePicker))
        {
            return null;
        }

        return Initialize<ContentCulturePickerSettings>("ContentCulturePickerSettings_Edit", model =>
        {
            model.SetCookie = settings.SetCookie;
            model.RedirectToHomepage = settings.RedirectToHomepage;
        }).Location("Content:5")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ContentCulturePickerSettings section, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, ContentLocalizationPermissions.ManageContentCulturePicker))
        {
            return null;
        }

        await context.Updater.TryUpdateModelAsync(section, Prefix);

        return await EditAsync(site, section, context);
    }
}
