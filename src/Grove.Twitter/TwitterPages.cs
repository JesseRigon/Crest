using Grove.Routing;

namespace Grove.Twitter;

/// <summary>
/// The X (Twitter) integration settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class TwitterPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("twitter", "X (Twitter)", "settings-integrations", TwitterConstants.Features.Twitter, [Permissions.ManageTwitter.Name]);
    }
}
