using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Crest.DisplayManagement.Entities;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Seo;
using Crest.Settings;
using Crest.Sitemaps.Models;

namespace Crest.Sitemaps.Drivers;

public sealed class SitemapsRobotsSettingsDisplayDriver : SiteDisplayDriver<SitemapsRobotsSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public SitemapsRobotsSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => SeoConstants.RobotsSettingsGroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, SitemapsRobotsSettings settings, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, SeoConstants.ManageSeoSettings))
        {
            return null;
        }

        return Initialize<SitemapsRobotsSettings>("SitemapsRobotsSettings_Edit", model =>
        {
            model.IncludeSitemaps = settings.IncludeSitemaps;
        }).Location("Content:4")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, SitemapsRobotsSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, SeoConstants.ManageSeoSettings))
        {
            return null;
        }

        await context.Updater.TryUpdateModelAsync(settings, Prefix);

        return await EditAsync(site, settings, context);
    }
}
