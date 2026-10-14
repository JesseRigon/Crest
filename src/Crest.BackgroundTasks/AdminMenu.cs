using Crest.Admin;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Crest.Navigation;

namespace Crest.BackgroundTasks;

public sealed class AdminMenu : AdminNavigationProvider
{
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
                .Add(S["Configuration"], configuration => configuration
                    .Add(S["Tasks"], S["Tasks"].PrefixPosition(), tasks => tasks
                        .Add(S["Background Tasks"], S["Background Tasks"].PrefixPosition(), backgroundTasks => backgroundTasks
                            .Url($"{_adminPath}/BackgroundTasks")
                            .Permission(Permissions.ManageBackgroundTasks)
                            .LocalNav()
                        )
                    )
                );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Tools"], tools => tools
                .Add(S["Background Tasks"], S["Background Tasks"].PrefixPosition(), backgroundTasks => backgroundTasks
                    .Url($"{_adminPath}/BackgroundTasks")
                    .Permission(Permissions.ManageBackgroundTasks)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
