using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Crest.Navigation;
using Crest.Users.Drivers;

namespace Crest.Users;

public sealed class ResetPasswordAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary s_routeValues = new()
    {
        { "area", "Crest.Settings" },
        { "groupId", ResetPasswordSettingsDisplayDriver.GroupId },
    };

    internal readonly IStringLocalizer S;

    public ResetPasswordAdminMenu(IStringLocalizer<ResetPasswordAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (NavigationHelper.UseLegacyFormat())
        {
            builder
                .Add(S["Security"], security => security
                    .Add(S["Settings"], settings => settings
                        .Add(S["User Reset Password"], S["User Reset Password"].PrefixPosition(), password => password
                            .Permission(UsersPermissions.ManageUsers)
                            .Action("Index", "Admin", s_routeValues)
                            .LocalNav()
                        )
                    )
                );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Security"], S["Security"].PrefixPosition(), security => security
                    .Add(S["Reset Password"], S["Reset Password"].PrefixPosition(), password => password
                        .Permission(UsersPermissions.ManageUsers)
                        .Action("Index", "Admin", s_routeValues)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
