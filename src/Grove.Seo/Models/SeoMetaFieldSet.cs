using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;
using Grove.Media.Fields;
using Grove.Display.Resources.Management;

namespace Grove.Seo.Models;

/// <summary>
/// An item's search and social metadata. Its data is fields (text fields, a boolean field
/// <c>Render</c>, media fields for the images); this class is the typed accessor over them. The custom
/// meta tags stay a list of entries on the field set (no field type holds a list of name/content
/// pairs). Checking the canonical address and the schema is the module's system flow, and the
/// metadata a read projection (docs/content-engine.md › Behaviour is workflows).
/// </summary>
public class SeoMetaFieldSet : ContentFieldSet
{
    public static readonly char[] InvalidCharactersForCanoncial = "?#[]@!$&'()*+,;=<>\\|%".ToCharArray();

    public static readonly string[] TextFields = ["PageTitle", "MetaDescription", "MetaKeywords", "Canonical", "MetaRobots", "OpenGraphType", "OpenGraphTitle", "OpenGraphDescription", "TwitterTitle", "TwitterDescription", "TwitterCard", "TwitterCreator", "TwitterSite", "GoogleSchema"];

    public static readonly string[] MediaFields = ["DefaultSocialImage", "OpenGraphImage", "TwitterImage"];

    public const string RenderField = "Render";

    /// <summary>Whether the meta tags render; true unless set off.</summary>
    [JsonIgnore]
    public bool Render
    {
        get => this.Get<BooleanField>(RenderField)?.Value ?? true;
        set => this.Alter<BooleanField>(RenderField, flag => flag.Value = value);
    }

    public MetaEntry[] CustomMetaTags { get; set; } = [];

    [JsonIgnore]
    public string PageTitle
    {
        get => this.Get<TextField>(nameof(PageTitle))?.Text;
        set => this.Alter<TextField>(nameof(PageTitle), text => text.Text = value);
    }

    [JsonIgnore]
    public string MetaDescription
    {
        get => this.Get<TextField>(nameof(MetaDescription))?.Text;
        set => this.Alter<TextField>(nameof(MetaDescription), text => text.Text = value);
    }

    [JsonIgnore]
    public string MetaKeywords
    {
        get => this.Get<TextField>(nameof(MetaKeywords))?.Text;
        set => this.Alter<TextField>(nameof(MetaKeywords), text => text.Text = value);
    }

    [JsonIgnore]
    public string Canonical
    {
        get => this.Get<TextField>(nameof(Canonical))?.Text;
        set => this.Alter<TextField>(nameof(Canonical), text => text.Text = value);
    }

    [JsonIgnore]
    public string MetaRobots
    {
        get => this.Get<TextField>(nameof(MetaRobots))?.Text;
        set => this.Alter<TextField>(nameof(MetaRobots), text => text.Text = value);
    }

    [JsonIgnore]
    public string OpenGraphType
    {
        get => this.Get<TextField>(nameof(OpenGraphType))?.Text;
        set => this.Alter<TextField>(nameof(OpenGraphType), text => text.Text = value);
    }

    [JsonIgnore]
    public string OpenGraphTitle
    {
        get => this.Get<TextField>(nameof(OpenGraphTitle))?.Text;
        set => this.Alter<TextField>(nameof(OpenGraphTitle), text => text.Text = value);
    }

    [JsonIgnore]
    public string OpenGraphDescription
    {
        get => this.Get<TextField>(nameof(OpenGraphDescription))?.Text;
        set => this.Alter<TextField>(nameof(OpenGraphDescription), text => text.Text = value);
    }

    [JsonIgnore]
    public string TwitterTitle
    {
        get => this.Get<TextField>(nameof(TwitterTitle))?.Text;
        set => this.Alter<TextField>(nameof(TwitterTitle), text => text.Text = value);
    }

    [JsonIgnore]
    public string TwitterDescription
    {
        get => this.Get<TextField>(nameof(TwitterDescription))?.Text;
        set => this.Alter<TextField>(nameof(TwitterDescription), text => text.Text = value);
    }

    [JsonIgnore]
    public string TwitterCard
    {
        get => this.Get<TextField>(nameof(TwitterCard))?.Text;
        set => this.Alter<TextField>(nameof(TwitterCard), text => text.Text = value);
    }

    [JsonIgnore]
    public string TwitterCreator
    {
        get => this.Get<TextField>(nameof(TwitterCreator))?.Text;
        set => this.Alter<TextField>(nameof(TwitterCreator), text => text.Text = value);
    }

    [JsonIgnore]
    public string TwitterSite
    {
        get => this.Get<TextField>(nameof(TwitterSite))?.Text;
        set => this.Alter<TextField>(nameof(TwitterSite), text => text.Text = value);
    }

    [JsonIgnore]
    public string GoogleSchema
    {
        get => this.Get<TextField>(nameof(GoogleSchema))?.Text;
        set => this.Alter<TextField>(nameof(GoogleSchema), text => text.Text = value);
    }

    [JsonIgnore]
    public MediaField DefaultSocialImage
    {
        get => this.Get<MediaField>(nameof(DefaultSocialImage));
        set
        {
            if (value is null)
            {
                this.Remove(nameof(DefaultSocialImage));
            }
            else
            {
                this.Apply(nameof(DefaultSocialImage), value);
            }
        }
    }

    [JsonIgnore]
    public MediaField OpenGraphImage
    {
        get => this.Get<MediaField>(nameof(OpenGraphImage));
        set
        {
            if (value is null)
            {
                this.Remove(nameof(OpenGraphImage));
            }
            else
            {
                this.Apply(nameof(OpenGraphImage), value);
            }
        }
    }

    [JsonIgnore]
    public MediaField TwitterImage
    {
        get => this.Get<MediaField>(nameof(TwitterImage));
        set
        {
            if (value is null)
            {
                this.Remove(nameof(TwitterImage));
            }
            else
            {
                this.Apply(nameof(TwitterImage), value);
            }
        }
    }
}
