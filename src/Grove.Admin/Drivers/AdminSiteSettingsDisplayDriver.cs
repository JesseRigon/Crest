using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Grove.Admin.Models;
using Grove.Admin.ViewModels;
using Grove.DisplayManagement.Entities;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Settings;

namespace Grove.Admin.Drivers;

public sealed class AdminSiteSettingsDisplayDriver : SiteDisplayDriver<AdminSettings>
{
    public const string GroupId = "admin";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public AdminSiteSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override async Task<IDisplayResult> EditAsync(ISite site, AdminSettings settings, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, AdminPermissions.ManageAdminSettings))
        {
            return null;
        }

        return Initialize<AdminSettingsViewModel, AdminSettings>("AdminSettings_Edit", static (model, settings) =>
        {
            model.DisplayThemeToggler = settings.DisplayThemeToggler;
            model.DisplayMenuFilter = settings.DisplayMenuFilter;
            model.DisplayNewMenu = settings.DisplayNewMenu;
            model.DisplayTitlesInTopbar = settings.DisplayTitlesInTopbar;
        }, settings).Location("Content:3")
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, AdminSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, AdminPermissions.ManageAdminSettings))
        {
            return null;
        }

        var model = new AdminSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.DisplayThemeToggler = model.DisplayThemeToggler;
        settings.DisplayMenuFilter = model.DisplayMenuFilter;
        settings.DisplayNewMenu = model.DisplayNewMenu;
        settings.DisplayTitlesInTopbar = model.DisplayTitlesInTopbar;

        return await EditAsync(site, settings, context);
    }
}
