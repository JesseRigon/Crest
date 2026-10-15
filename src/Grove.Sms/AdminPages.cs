using Grove.Mvc.Utilities;
using Grove.Routing;
using Grove.Sms.Controllers;

namespace Grove.Sms;

/// <summary>
/// The SMS settings and test page: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("sms", "SMS", "settings-communication", SmsSettings.GroupId, [SmsPermissions.ManageSmsSettings.Name]);
        yield return PageEntries.Legacy("smstest", "SMS Test", "testing", "Grove.Sms", "Admin", "Test", [SmsPermissions.ManageSmsSettings.Name]);
    }
}
