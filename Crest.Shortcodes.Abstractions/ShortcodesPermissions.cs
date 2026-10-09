using Crest.Security.Permissions;

namespace Crest.Shortcodes;

public static class ShortcodesPermissions
{
    public static readonly Permission ManageShortcodeTemplates = new("ManageShortcodeTemplates", "Manage shortcode templates", isSecurityCritical: true);
}
