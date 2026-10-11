using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Crest.Mvc.Utilities;
using Crest.Navigation;
using Crest.Users.AuditTrail.Controllers;
using Crest.Users.Drivers;
using Crest.Users.Models;

namespace Crest.Users.AuditTrail;

public sealed class AdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary s_routeValues = new()
    {
        { "area", "Crest.Settings" },
        { "groupId", LoginSettingsDisplayDriver.GroupId },
    };

    internal readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Security"], S["Security"].PrefixPosition(), security => security
                    .Add(S["User Audit Trail"], S["User Audit Trail"].PrefixPosition(), login => login
                        .Permission(Permissions.ManageUserAuditTrailSettings)
                        .Action(
                            nameof(AuditTrailAdminController.Index),
                            typeof(AuditTrailAdminController).ControllerName(),
                            new { Area = "Crest.Users" })
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
