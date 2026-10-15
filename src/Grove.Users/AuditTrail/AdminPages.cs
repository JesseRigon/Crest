using Grove.Mvc.Utilities;
using Grove.Routing;
using Grove.Users.AuditTrail.Controllers;
using Grove.Users.Drivers;
using Grove.Users.Models;

namespace Grove.Users.AuditTrail;

/// <summary>
/// The user audit trail settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Legacy("user-audit-trail", "User Audit Trail", "settings-security", "Grove.Users", "AuditTrailAdmin", "Index", [Permissions.ManageUserAuditTrailSettings.Name]);
    }
}
