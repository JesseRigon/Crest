using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Seo.Sitemaps.Models;

/// <summary>
/// An item's sitemap entry, which can exclude it or override the sitemap's frequency and priority.
/// Its data is fields (<c>OverrideSitemapConfig</c> and <c>Exclude</c>, boolean fields;
/// <c>ChangeFrequency</c>, a text field; <c>Priority</c>, a numeric field); this class is the typed
/// accessor over them. The entry is a read projection (docs/content-engine.md › Behaviour is workflows).
/// </summary>
public class SitemapFieldSet : ContentFieldSet
{
    [JsonIgnore]
    public bool OverrideSitemapConfig
    {
        get => this.Get<BooleanField>(nameof(OverrideSitemapConfig))?.Value ?? false;
        set => this.Alter<BooleanField>(nameof(OverrideSitemapConfig), flag => flag.Value = value);
    }

    [JsonIgnore]
    public ChangeFrequency ChangeFrequency
    {
        get => Enum.TryParse<ChangeFrequency>(this.Get<TextField>(nameof(ChangeFrequency))?.Text, ignoreCase: true, out var frequency) ? frequency : ChangeFrequency.Daily;
        set => this.Alter<TextField>(nameof(ChangeFrequency), text => text.Text = value.ToString());
    }

    [JsonIgnore]
    public int Priority
    {
        get => this.Get<NumericField>(nameof(Priority))?.Value is { } priority ? (int)priority : 5;
        set => this.Alter<NumericField>(nameof(Priority), number => number.Value = value);
    }

    [JsonIgnore]
    public bool Exclude
    {
        get => this.Get<BooleanField>(nameof(Exclude))?.Value ?? false;
        set => this.Alter<BooleanField>(nameof(Exclude), flag => flag.Value = value);
    }
}
