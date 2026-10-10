using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Crest.Layers.Drivers;
using Crest.Navigation;

namespace Crest.Layers;

public sealed class AdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary s_routeValues = new()
    {
        { "area", "Crest.Settings" },
        { "groupId", LayerSiteSettingsDisplayDriver.GroupId },
    };

    internal readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (NavigationHelper.UseLegacyFormat())
        {
            builder
                .Add(S["Design"], design => design
                    .Add(S["Settings"], settings => settings
                        .Add(S["Zones"], S["Zones"].PrefixPosition(), zones => zones
                            .Action("Index", "Admin", s_routeValues)
                            .Permission(Permissions.ManageLayers)
                            .LocalNav()
                        )
                    )
                    .Add(S["Widgets"], S["Widgets"].PrefixPosition(), widgets => widgets
                        .Permission(Permissions.ManageLayers)
                        .Action("Index", "Admin", "Crest.Layers")
                        .LocalNav()
                    )
                );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Design"], design => design
                .Add(S["Widgets"], S["Widgets"].PrefixPosition(), widgets => widgets
                    .Permission(Permissions.ManageLayers)
                    .Action("Index", "Admin", "Crest.Layers")
                    .LocalNav()
                )
            )
            .Add(S["Settings"], settings => settings
                .Add(S["Zones"], S["Zones"].PrefixPosition(), zones => zones
                    .Action("Index", "Admin", s_routeValues)
                    .Permission(Permissions.ManageLayers)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
