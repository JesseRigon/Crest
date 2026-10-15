using Grove.AuditTrail.Api;
using Grove.Routing;

namespace Grove.AuditTrail.Navigation;

/// <summary>
/// The Audit Trail section under Tools: it opens the trail, and its pages (events, impersonation
/// sessions) declare themselves on their components.
/// </summary>
public sealed class AuditTrailPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages() =>
    [
        new PageEntry
        {
            Id = "audittrail",
            Permissions = [AuditTrailPermissions.ViewAuditTrail.Name],
            Menu = new PageMenu("Audit Trail", "tools", Icon: "@iconify:mdi:history") { Url = AuditTrailRoutes.TrailAdmin },
        },
    ];
}
