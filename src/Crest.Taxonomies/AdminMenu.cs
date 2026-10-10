using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Crest.Navigation;
using Crest.Taxonomies.Settings;

namespace Crest.Taxonomies;

public sealed class AdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary s_routeValues = new()
    {
        { "area", "Crest.Settings" },
        { "groupId", TaxonomyContentsAdminListSettingsDisplayDriver.GroupId },
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
                .Add(S["Settings"], "1", settings => settings
                    .Add(S["Taxonomy Filters"], S["Taxonomy Filters"].PrefixPosition(), filters => filters
                        .AddClass("taxonomyfilters")
                        .Id("taxonomyfilters")
                        .Permission(Permissions.ManageTaxonomies)
                        .Action("Index", "Admin", s_routeValues)
                        .LocalNav()
                    )
                )
            );

            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Taxonomy Filters"], S["Taxonomy Filters"].PrefixPosition(), filters => filters
                    .AddClass("taxonomyfilters")
                    .Id("taxonomyfilters")
                    .Permission(Permissions.ManageTaxonomies)
                    .Action("Index", "Admin", s_routeValues)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
