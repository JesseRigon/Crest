using Microsoft.Extensions.Localization;
using Crest.Indexing;
using Crest.Admin;
using Crest.Navigation;
using Microsoft.Extensions.Options;

namespace Crest.Indexing;

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
        builder
            .Add(S["Search"], NavigationConstants.AdminMenuSearchPosition, search => search
                .AddClass("search")
                .Id("search")
                .Add(S["Indexes"], S["Indexes"].PrefixPosition(), indexes => indexes
                    .Url($"{_adminPath}/indexing")
                    .AddClass("indexes")
                    .Id("indexes")
                    .Permission(IndexingPermissions.ManageIndexes)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
