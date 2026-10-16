using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.Hierarchy.Models;

/// <summary>
/// A list of child items. Its own data is its feed's fields (<c>DisableRssFeed</c>, a boolean field;
/// <c>FeedProxyUrl</c>, a text field; <c>FeedItemsCount</c>, a numeric field); this class is the typed
/// accessor over them. Moving translated children with a translated list is the module's system flow,
/// and the feed's metadata a read projection (docs/content-engine.md › Behaviour is workflows).
/// </summary>
public class ChildContentItemsFieldSet : ContentFieldSet
{
    [JsonIgnore]
    public bool DisableRssFeed
    {
        get => this.Get<BooleanField>(nameof(DisableRssFeed))?.Value ?? false;
        set => this.Alter<BooleanField>(nameof(DisableRssFeed), flag => flag.Value = value);
    }

    [JsonIgnore]
    public string FeedProxyUrl
    {
        get => this.Get<TextField>(nameof(FeedProxyUrl))?.Text;
        set => this.Alter<TextField>(nameof(FeedProxyUrl), text => text.Text = value);
    }

    [JsonIgnore]
    public int? FeedItemsCount
    {
        get => this.Get<NumericField>(nameof(FeedItemsCount))?.Value is { } count ? (int)count : null;
        set => this.Alter<NumericField>(nameof(FeedItemsCount), number => number.Value = value);
    }
}
