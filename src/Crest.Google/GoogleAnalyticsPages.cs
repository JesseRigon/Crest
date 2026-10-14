using Crest.Routing;

namespace Crest.Google;

/// <summary>
/// The Google Analytics settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class GoogleAnalyticsPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("googleAnalytics", "Google Analytics", "settings-integrations", GoogleConstants.Features.GoogleAnalytics, [Permissions.ManageGoogleAnalytics.Name]);
    }
}
