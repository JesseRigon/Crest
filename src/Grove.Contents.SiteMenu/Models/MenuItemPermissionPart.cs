using Grove.Contents.Management;

namespace Grove.Contents.SiteMenu.Models;

public class MenuItemPermissionPart : ContentPart
{
    /// <summary>
    /// The names of the permissions required to view the menu item.
    /// </summary>
    public string[] PermissionNames { get; set; } = [];
}
