using Grove.Routing;

namespace Grove.Display.Navigation;

/// <summary>
/// The admin menu's shared groups (page registry entries with no page of their own): the
/// top-level sections and the sub-sections several modules put pages under. A module's own
/// section (Workflows, Parties, Audit Trail) is declared by that module. A group with no visible
/// child is not shown.
/// </summary>
public sealed class AdminMenuGroups : IPageSource
{
    public IEnumerable<PageEntry> GetPages() =>
    [
        PageEntries.Group("content", "Content", position: "1", icon: "@iconify:mdi:file-document-multiple"),
        PageEntries.Group("design", "Design", position: "2", icon: "@iconify:mdi:palette"),
        PageEntries.Group("content-definition", "Content Definition", "design", icon: "@iconify:mdi:shape-outline"),
        PageEntries.Group("search", "Search", position: "6", icon: "@iconify:mdi:magnify"),
        PageEntries.Group("queries", "Queries", "search", icon: "@iconify:mdi:format-list-bulleted"),
        PageEntries.Group("accessControl", "Access Control", position: "7", icon: "@iconify:mdi:shield-account"),
        PageEntries.Group("openid", "OpenID Connect", "accessControl"),
        PageEntries.Group("media", "Media", position: "after.15", icon: "@iconify:mdi:image-multiple"),
        PageEntries.Group("multitenancy", "Multi-Tenancy", position: "after.25", icon: "@iconify:mdi:home-city"),
        PageEntries.Group("tools", "Tools", position: "end.50", icon: "@iconify:mdi:tools"),
        PageEntries.Group("deployments", "Deployments", "tools", icon: "@iconify:mdi:cloud-upload"),
        PageEntries.Group("testing", "Testing", "tools"),
        PageEntries.Group("settings", "Settings", position: "end.100", icon: "@iconify:mdi:cog"),
        PageEntries.Group("settings-communication", "Communication", "settings"),
        PageEntries.Group("settings-integrations", "Integrations", "settings"),
        PageEntries.Group("settings-localization", "Localization", "settings", icon: "@iconify:mdi:translate"),
        PageEntries.Group("settings-media", "Media", "settings"),
        PageEntries.Group("settings-search", "Search", "settings"),
        PageEntries.Group("settings-security", "Security", "settings", icon: "@iconify:mdi:security"),
        PageEntries.Group("settings-authentication", "Authentication", "settings-security"),
        PageEntries.Group("openid-settings", "OpenID Connect", "settings"),
    ];
}
