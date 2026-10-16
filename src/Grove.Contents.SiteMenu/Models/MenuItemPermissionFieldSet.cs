using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.SiteMenu.Models;

/// <summary>
/// The permissions a menu item requires to be shown (a multi-text field). This class is the typed accessor over the fields (docs/content-engine.md › Behaviour is
/// workflows).
/// </summary>
public class MenuItemPermissionFieldSet : ContentFieldSet
{
    /// <summary>
    /// The names of the permissions required to view the menu item.
    /// </summary>
    [JsonIgnore]
    public string[] PermissionNames
    {
        get => this.Get<MultiTextField>(nameof(PermissionNames))?.Values ?? [];
        set => this.Alter<MultiTextField>(nameof(PermissionNames), fieldValue => fieldValue.Values = value);
    }
}
