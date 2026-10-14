using Crest.Mvc.Utilities;
using Crest.Routing;
using Crest.Users.AuditTrail.Controllers;
using Crest.Users.Drivers;
using Crest.Users.Models;

namespace Crest.Users.AuditTrail;

/// <summary>
/// The user audit trail settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Legacy("user-audit-trail", "User Audit Trail", "settings-security", "Crest.Users", "AuditTrailAdmin", "Index", [Permissions.ManageUserAuditTrailSettings.Name]);
    }
}
