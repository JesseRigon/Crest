using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Crest.Admin;
using Crest.Navigation;
using Microsoft.Extensions.Options;
using Crest.Users.Drivers;
using Crest.Users.Models;

namespace Crest.Users;

public sealed class AdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary s_routeValues = new()
    {
        { "area", "Crest.Settings" },
        { "groupId", LoginSettingsDisplayDriver.GroupId },
    };

    internal readonly IStringLocalizer S;

    private readonly string _adminPath;

    public AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions)
    {
        S = stringLocalizer;
        _adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (NavigationHelper.UseLegacyFormat())
        {
            builder
                .Add(S["Security"], NavigationConstants.AdminMenuSecurityPosition, security => security
                    .AddClass("security")
                    .Id("security")
                    .Add(S["Users"], S["Users"].PrefixPosition(), users => users
                        .AddClass("users")
                        .Id("users")
                        .Url($"{_adminPath}/Users/Index")
                        .Permission(UsersPermissions.ListUsers)
                        .Resource(new User())
                        .LocalNav()
                    )
                    .Add(S["Settings"], settings => settings
                        .Add(S["User Login"], S["User Login"].PrefixPosition(), login => login
                            .Permission(UsersPermissions.ManageUsers)
                            .Action("Index", "Admin", s_routeValues)
                            .LocalNav()
                        )
                    )
                );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Access Control"], NavigationConstants.AdminMenuAccessControlPosition, accessControl => accessControl
                .AddClass("accessControl")
                .Id("accessControl")
                .Add(S["Users"], S["Users"].PrefixPosition(), users => users
                    .AddClass("users")
                    .Id("users")
                    .Url($"{_adminPath}/Users/Index")
                    .Permission(UsersPermissions.ListUsers)
                    .Resource(new User())
                    .LocalNav()
                )
            , priority: 1)

            .Add(S["Settings"], settings => settings
                .Add(S["Security"], S["Security"].PrefixPosition(), security => security
                    .Add(S["User Login"], S["User Login"].PrefixPosition(), login => login
                        .Permission(UsersPermissions.ManageUsers)
                        .Action("Index", "Admin", s_routeValues)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
