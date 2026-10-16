using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.SiteMenu.Models;

/// <summary>
/// A content menu item: whether the caller's permissions on the content item are checked (a boolean field). This class is the typed accessor over the fields (docs/content-engine.md › Behaviour is
/// workflows).
/// </summary>
public class ContentMenuItemFieldSet : ContentFieldSet
{
    [JsonIgnore]
    public bool CheckContentPermissions
    {
        get => this.Get<BooleanField>(nameof(CheckContentPermissions))?.Value ?? false;
        set => this.Alter<BooleanField>(nameof(CheckContentPermissions), fieldValue => fieldValue.Value = value);
    }
}
