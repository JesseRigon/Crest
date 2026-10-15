using Grove.ContentManagement;

namespace Grove.Menu.Models;

public class MenuItemPermissionPart : ContentPart
{
    /// <summary>
    /// The names of the permissions required to view the menu item.
    /// </summary>
    public string[] PermissionNames { get; set; } = [];
}
