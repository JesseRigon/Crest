using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.Hierarchy.Models;

/// <summary>
/// Welded on a content item instance when it's part of a list. Its data is fields
/// (<c>ListContentItemId</c> and <c>ListContentType</c>, text fields; <c>Order</c>, a numeric field);
/// this class is the typed accessor over them. Filing the item in its list, its order and the list in
/// its culture are the module's system flows (docs/content-engine.md › Behaviour is workflows).
/// </summary>
public class ParentContentItemFieldSet : ContentFieldSet
{
    public const string ListContentItemIdField = "ListContentItemId";
    public const string ListContentTypeField = "ListContentType";
    public const string OrderField = "Order";

    /// <summary>
    /// The content item id of the list owning this content item.
    /// </summary>
    [JsonIgnore]
    public string ListContentItemId
    {
        get => this.Get<TextField>(ListContentItemIdField)?.Text;
        set => this.Alter<TextField>(ListContentItemIdField, text => text.Text = value);
    }

    /// <summary>
    /// The content type of the list owning this content item.
    /// </summary>
    [JsonIgnore]
    public string ListContentType
    {
        get => this.Get<TextField>(ListContentTypeField)?.Text;
        set => this.Alter<TextField>(ListContentTypeField, text => text.Text = value);
    }

    /// <summary>
    /// The order of this content item in the list.
    /// </summary>
    [JsonIgnore]
    public int Order
    {
        get => this.Get<NumericField>(OrderField)?.Value is { } order ? (int)order : 0;
        set => this.Alter<NumericField>(OrderField, number => number.Value = value);
    }
}
