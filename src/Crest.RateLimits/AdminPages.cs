using Crest.RateLimits;
using Crest.Routing;

namespace Crest.RateLimits;

/// <summary>
/// Rate limits: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Legacy("rate-limits", "Rate Limits", "tools", "Crest.RateLimits", "Admin", "Index", [RateLimitsPermissions.ManageRateLimits.Name]);
    }
}
