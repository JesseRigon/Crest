using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Crest.Mvc.Core.Utilities;
using Crest.Navigation;
using Crest.Sms.Controllers;

namespace Crest.Sms;

public sealed class AdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary s_routeValues = new()
    {
        { "area", "Crest.Settings" },
        { "groupId", SmsSettings.GroupId },
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
                        .Add(S["SMS"], S["SMS"].PrefixPosition(), sms => sms
                            .AddClass("sms")
                            .Id("sms")
                            .Action("Index", "Admin", s_routeValues)
                            .Permission(SmsPermissions.ManageSmsSettings)
                            .LocalNav()
                        )
                        .Add(S["SMS Test"], S["SMS Test"].PrefixPosition(), sms => sms
                            .AddClass("smstest")
                            .Id("smstest")
                            .Action(nameof(AdminController.Test), typeof(AdminController).ControllerName(), "Crest.Sms")
                            .Permission(SmsPermissions.ManageSmsSettings)
                            .LocalNav()
                        )
                    )
                );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Communication"], S["Communication"].PrefixPosition(), communication => communication
                    .Add(S["SMS"], S["SMS"].PrefixPosition(), sms => sms
                        .AddClass("sms")
                        .Id("sms")
                        .Action("Index", "Admin", s_routeValues)
                        .Permission(SmsPermissions.ManageSmsSettings)
                        .LocalNav()
                    )
                )
            )
            .Add(S["Tools"], tools => tools
                .Add(S["Testing"], S["Testing"].PrefixPosition(), testing => testing
                    .Add(S["SMS Test"], S["SMS Test"].PrefixPosition(), sms => sms
                        .AddClass("smstest")
                        .Id("smstest")
                        .Action(nameof(AdminController.Test), typeof(AdminController).ControllerName(), "Crest.Sms")
                        .Permission(SmsPermissions.ManageSmsSettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
