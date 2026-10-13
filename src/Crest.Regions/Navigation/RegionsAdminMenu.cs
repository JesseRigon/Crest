using Crest.Admin;
using Crest.Data.Global;
using Crest.Environment.Shell;
using Crest.Navigation;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Crest.Regions.Navigation;

/// <summary>
/// Settings › Addressing maps, on the Default tenant only: the maps are global reference data
/// the Default tenant edits (docs/regions.md › Addressing maps as runtime rules). A
/// Blazor-only page, so a .Url on the real admin prefix (as Crest's own Blazor-only entries).
/// </summary>
public sealed class RegionsAdminMenu(IStringLocalizer<RegionsAdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions, ShellSettings shellSettings) : AdminNavigationProvider
{
    public const string AddressingMapsRoute = "/Regions/AddressingMaps";

    private readonly IStringLocalizer S = stringLocalizer;

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (!shellSettings.IsDefaultShell())
        {
            return ValueTask.CompletedTask;
        }

        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');
        builder.Add(S["Settings"], settings => settings
            .Add(S["Addressing maps"], S["Addressing maps"].PrefixPosition(), maps => maps
                .AddClass("addressing-maps")
                .Id("addressing-maps")
                .Url(adminPath + AddressingMapsRoute)
                .Permission(GlobalPermissions.ManageGlobalReferenceData)
                .LocalNav()
            ));

        return ValueTask.CompletedTask;
    }
}

/// <summary>The addressing-map editor's page gate.</summary>
public sealed class RegionsRoutePermissionProvider : Crest.Access.IRoutePermissionProvider
{
    public IEnumerable<Crest.Access.RoutePermission> GetRoutes() =>
    [
        new(RegionsAdminMenu.AddressingMapsRoute, GlobalPermissions.ManageGlobalReferenceData.Name),
    ];
}
