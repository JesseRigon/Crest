using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Crest.Navigation;
using Crest.RateLimits.Core;
namespace Crest.RateLimits;

internal sealed class AdminMenu : AdminNavigationProvider
{
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
                .Add(S["Tools"], tools => tools
                    .Add(S["Rate Limits"], S["Rate Limits"].PrefixPosition(), rateLimits => rateLimits
                        .Action("Index", "Admin", "Crest.RateLimits")
                        .Permission(RateLimitsPermissions.ManageRateLimits)
                        .LocalNav()
                    )
                );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Tools"], tools => tools
                .Add(S["Rate Limits"], S["Rate Limits"].PrefixPosition(), rateLimits => rateLimits
                    .Action("Index", "Admin", "Crest.RateLimits")
                    .Permission(RateLimitsPermissions.ManageRateLimits)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
