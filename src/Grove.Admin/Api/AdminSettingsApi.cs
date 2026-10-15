#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Admin;
using Grove.Admin.Models;
using Grove.Entities;
using Grove.Settings;

namespace Grove.Admin.Api;

public sealed class AdminSettingsApi : ApiContributor
{
    public override string Name => "AdminSettings";

    public override string FeatureId => "Grove.Admin";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("admin-settings");
        routes.MapGet("", ApiHandlers.Handle<AdminSettingsHandlers>(h => h.GetAsync()));
        routes.MapPut("", (HttpContext __http, [FromBody] AdminSettingsUpdate update) => ApiHandlers.For<AdminSettingsHandlers>(__http).PutAsync(update));
    }
}

public sealed class AdminSettingsHandlers(
    IAuthorizationService authorizationService,
    ISiteService siteService) : ApiHandlers
{
    public async Task<IResult> GetAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings))
        {
            return Forbid();
        }

        var site = await siteService.GetSiteSettingsAsync();
        return Ok(AdminSettingsDto.From(site.GetOrCreate<AdminSettings>()));
    }

    public async Task<IResult> PutAsync(AdminSettingsUpdate update)
    {
        if (!await authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings))
        {
            return Forbid();
        }

        var site = await siteService.LoadSiteSettingsAsync();
        var updated = new AdminSettings
        {
            DisplayThemeToggler = update.DisplayThemeToggler,
            DisplayMenuFilter = update.DisplayMenuFilter,
            DisplayNewMenu = update.DisplayNewMenu,
            DisplayTitlesInTopbar = update.DisplayTitlesInTopbar,
        };

        site.Alter<AdminSettings>(settings =>
        {
            settings.DisplayThemeToggler = updated.DisplayThemeToggler;
            settings.DisplayMenuFilter = updated.DisplayMenuFilter;
            settings.DisplayNewMenu = updated.DisplayNewMenu;
            settings.DisplayTitlesInTopbar = updated.DisplayTitlesInTopbar;
        });

        await siteService.UpdateSiteSettingsAsync(site);

        return Ok(AdminSettingsDto.From(updated));
    }
}
