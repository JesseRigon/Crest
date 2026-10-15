using Grove.ReCaptcha.Drivers;
using Grove.Routing;

namespace Grove.ReCaptcha;

/// <summary>
/// The reCaptcha settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("recaptcha", "reCaptcha", "settings-security", ReCaptchaSettingsDisplayDriver.GroupId, [ReCaptchaPermissions.ManageReCaptchaSettings.Name]);
    }
}
