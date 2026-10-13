using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Crest.Admin;
using Crest.AuditTrail.Controllers;
using Crest.AuditTrail.Settings;
using Crest.Navigation;

namespace Crest.AuditTrail.Navigation;

public sealed class AuditTrailAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary s_routeValues = new()
    {
        { "area", "Crest.AuditTrail" },
        { "correlationId", string.Empty },
    };

    private static readonly RouteValueDictionary s_settingsRouteValues = new()
    {
        { "area", "Crest.Settings" },
        { "groupId", AuditTrailSettingsGroup.Id },
    };

    internal readonly IStringLocalizer S;
    private readonly string _adminPath;

    public AuditTrailAdminMenu(IStringLocalizer<AuditTrailAdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions)
    {
        S = stringLocalizer;
        _adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (NavigationHelper.UseLegacyFormat())
        {
            builder
                .Add(S["Audit Trail"], NavigationConstants.AdminMenuAuditTrailPosition, configuration => configuration
                    .AddClass("audittrail")
                    .Id("audittrail")
                    .Action(nameof(AdminController.Index), "Admin", s_routeValues)
                    .Permission(AuditTrailPermissions.ViewAuditTrail)
                    .LocalNav()
                , priority: 1)
                .Add(S["Configuration"], configuration => configuration
                     .Add(S["Settings"], settings => settings
                        .Add(S["Audit Trail"], S["Audit Trail"].PrefixPosition(), auditTrail => auditTrail
                            .AddClass("audittrail")
                            .Id("audittrailSettings")
                            .Action("Index", "Admin", s_routeValues)
                            .Permission(AuditTrailPermissions.ManageAuditTrailSettings)
                            .LocalNav()
                        )
                    )
                );

            return ValueTask.CompletedTask;
        }

        // The Crest audit pages (Crest.AuditTrail.BlazorWasm, docs/audit.md › step 4).
        builder
            .Add(S["Tools"], tools => tools
                .Add(S["Audit Trail"], S["Audit Trail"].PrefixPosition(), configuration => configuration
                    .AddClass("audittrail")
                    .Id("audittrail")
                    .Url(_adminPath + Api.AuditTrailRoutes.TrailAdmin)
                    .Permission(AuditTrailPermissions.ViewAuditTrail)
                    .LocalNav()
                    .Add(S["Events"], "0", events => events
                        .Id("audittrail-events")
                        .Url(_adminPath + Api.AuditTrailRoutes.TrailAdmin)
                        .Permission(AuditTrailPermissions.ViewAuditTrail)
                        .LocalNav())
                    .Add(S["Impersonation sessions"], "10", sessions => sessions
                        .Id("audittrail-sessions")
                        .Url(_adminPath + Api.AuditTrailRoutes.SessionsAdmin)
                        .Permission(AuditTrailPermissions.ViewAuditTrail)
                        .LocalNav())
                )
            )
            .Add(S["Settings"], settings => settings
                .Add(S["Audit Trail"], S["Audit Trail"].PrefixPosition(), auditTrail => auditTrail
                    .AddClass("audittrail")
                    .Id("audittrailSettings")
                    .Action("Index", "Admin", s_settingsRouteValues)
                    .Permission(AuditTrailPermissions.ManageAuditTrailSettings)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
