using Grove.CustomSettings.Services;
using Grove.Mvc.Utilities;
using Grove.Routing;

namespace Grove.CustomSettings;

/// <summary>
/// One Settings item per custom settings type (dynamic page registry entries, since types are
/// tenant data): the native custom settings page for that type, gated at its concrete route by
/// the type's own permission, the same one its API checks.
/// </summary>
public sealed class CustomSettingsPages(CustomSettingsService customSettingsService) : IDynamicPageSource
{
    public async Task<IEnumerable<PageEntry>> GetPagesAsync(PageSet declared, CancellationToken cancellationToken = default) =>
        (await customSettingsService.GetAllSettingsTypesAsync())
            .Select(type => new PageEntry
            {
                Id = type.Name.HtmlClassify(),
                Routes = [$"/CustomSettings/{type.Name}"],
                Permissions = [Permissions.CreatePermissionForType(type).Name],
                Menu = new PageMenu(type.DisplayName, "settings")
                {
                    Url = $"/CustomSettings/{Uri.EscapeDataString(type.Name)}",
                    CaptionIsData = true,
                },
            })
            .ToArray();
}
