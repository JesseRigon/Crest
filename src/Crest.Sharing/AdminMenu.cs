using Crest.Access;
using Crest.Admin;
using Crest.Navigation;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Crest.Sharing;

/// <summary>
/// The tenant's sharing (docs/sharing.md § 8, requirement 97): its entries and rules, which reach
/// every object, for holders of <see cref="Permissions.ManageTenantSharing"/>. A Blazor page with
/// no MVC action, so the link is built from the admin prefix.
/// </summary>
public sealed class AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions) : AdminNavigationProvider
{
    private readonly IStringLocalizer S = stringLocalizer;

    /// <summary>The page, relative to the admin.</summary>
    public const string Route = "/Sharing";

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');
        builder
            .Add(S["Access Control"], accessControl => accessControl
                .Add(S["Sharing"], S["Sharing"].PrefixPosition(), sharing => sharing
                    .AddClass("sharing")
                    .Id("sharing")
                    .Url($"{adminPath}{Route}")
                    .Permission(Permissions.ManageTenantSharing)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

/// <summary>The sharing pages: the tenant's (its permission) and any object's (the services
/// decide per object, so the page needs only the admin).</summary>
public sealed class SharingRoutePermissions : IRoutePermissionProvider
{
    public IEnumerable<RoutePermission> GetRoutes() =>
    [
        new(AdminMenu.Route, SharingPermissionNames.ManageTenantSharing),
        new($"{AdminMenu.Route}/{{Kind}}/{{Id}}", AdminPermissions.AccessAdminPanel.Name),
    ];
}
