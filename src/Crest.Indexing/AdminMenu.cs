using Microsoft.Extensions.Localization;
using Crest.Indexing;
using Crest.Navigation;

namespace Crest.Indexing;

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
            .Add(S["Search"], NavigationConstants.AdminMenuSearchPosition, search => search
                .AddClass("search")
                .Id("search")
                .Add(S["Indexes"], S["Indexes"].PrefixPosition(), indexes => indexes
                    .Action("Index", "Admin", "Crest.Indexing")
                    .AddClass("indexes")
                    .Id("indexes")
                    .Permission(IndexingPermissions.ManageIndexes)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
