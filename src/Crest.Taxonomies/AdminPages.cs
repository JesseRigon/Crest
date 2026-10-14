using Crest.Routing;
using Crest.Taxonomies.Settings;

namespace Crest.Taxonomies;

/// <summary>
/// The taxonomy filter settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("taxonomyfilters", "Taxonomy Filters", "settings", TaxonomyContentsAdminListSettingsDisplayDriver.GroupId, [Permissions.ManageTaxonomies.Name]);
    }
}
