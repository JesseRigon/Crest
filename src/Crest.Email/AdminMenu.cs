using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Crest.Email.Controllers;
using Crest.Mvc.Utilities;
using Crest.Navigation;

namespace Crest.Email;

public sealed class AdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary s_routeValues = new()
    {
        { "area", "Crest.Settings" },
        { "groupId", EmailSettings.GroupId },
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
                .Add(S["Configuration"], configuration => configuration
                    .Add(S["Settings"], settings => settings
                       .Add(S["Email"], S["Email"].PrefixPosition(), entry => entry
                          .AddClass("email")
                          .Id("email")
                          .Action("Index", "Admin", s_routeValues)
                          .Permission(EmailPermissions.ManageEmailSettings)
                          .LocalNav()
                        )
                       .Add(S["Email Test"], S["Email Test"].PrefixPosition(), entry => entry
                          .AddClass("emailtest")
                          .Id("emailtest")
                          .Action(nameof(AdminController.Test), typeof(AdminController).ControllerName(), "Crest.Email")
                          .Permission(EmailPermissions.ManageEmailSettings)
                          .LocalNav()
                        )
                    )
                );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Tools"], tools => tools
                .Add(S["Testing"], S["Testing"].PrefixPosition(), testing => testing
                    .Add(S["Email Test"], S["Email Test"].PrefixPosition(), entry => entry
                        .AddClass("emailtest")
                        .Id("emailtest")
                        .Action(nameof(AdminController.Test), typeof(AdminController).ControllerName(), "Crest.Email")
                        .Permission(EmailPermissions.ManageEmailSettings)
                        .LocalNav()
                    )
                )
            )
            .Add(S["Settings"], settings => settings
                .Add(S["Communication"], S["Communication"].PrefixPosition(), communication => communication
                    .Add(S["Email"], S["Email"].PrefixPosition(), entry => entry
                        .AddClass("email")
                        .Id("email")
                        .Action("Index", "Admin", s_routeValues)
                        .Permission(EmailPermissions.ManageEmailSettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
