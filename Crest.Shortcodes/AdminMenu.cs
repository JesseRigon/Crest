using Microsoft.Extensions.Localization;
using Crest.Navigation;

namespace Crest.Shortcodes;

public sealed class AdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Design"], design => design
                .Add(S["Shortcodes"], S["Shortcodes"].PrefixPosition(), import => import
                    .Action("Index", "Admin", "Crest.Shortcodes")
                    .Permission(ShortcodesPermissions.ManageShortcodeTemplates)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
