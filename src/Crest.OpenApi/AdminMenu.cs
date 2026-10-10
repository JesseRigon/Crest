using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Crest.Navigation;
using Crest.OpenApi.Drivers;

namespace Crest.OpenApi;

public sealed class AdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary s_routeValues = new()
    {
        { "area", "Crest.Settings" },
        { "groupId", OpenApiSettingsDisplayDriver.GroupId },
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
                .Add(S["OpenApi"], S["OpenApi"].PrefixPosition(), openApi => openApi
                    .Permission(OpenApiPermissions.ManageOpenApi)
                    .Action("Index", "Admin", s_routeValues)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
