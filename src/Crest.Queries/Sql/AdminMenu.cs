using Microsoft.Extensions.Localization;
using Crest.Navigation;

namespace Crest.Queries.Sql;

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
                .Add(S["Queries"], S["Queries"].PrefixPosition(), queries => queries
                    .Add(S["Run SQL Query"], S["Run SQL Query"].PrefixPosition(), sql => sql
                         .Action("Query", "Admin", "Crest.Queries")
                         .Permission(QueriesPermissions.ManageSqlQueries)
                         .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
