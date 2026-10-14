using Crest.Environment.Shell;
using Crest.Routing;

namespace Crest.Tenants;

/// <summary>
/// Feature profiles, on the Default tenant: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class FeatureProfilesPages(ShellSettings shellSettings) : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        if (!shellSettings.IsDefaultShell())
        {
            yield break;
        }

        yield return PageEntries.Legacy("feature-profiles", "Feature Profiles", "multitenancy", "Crest.Tenants", "FeatureProfiles", "Index", [Permissions.ManageTenantFeatureProfiles.Name]);
    }
}
