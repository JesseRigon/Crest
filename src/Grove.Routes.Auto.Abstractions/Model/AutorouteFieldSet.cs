using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Routes.Auto.Models;

/// <summary>
/// An item's permalink. Its data is fields (<c>Autoroute.Path</c>, a text field; <c>SetHomepage</c>,
/// <c>Disabled</c>, <c>RouteContainedItems</c> and <c>Absolute</c>, boolean fields); this class is the
/// typed accessor over them. Its behaviour (the path generated from the attachment's pattern, valid and
/// unique, the route entries and the home route kept on publish) is the module's system flows, and its
/// route values a read projection (docs/content-engine.md › Behaviour is workflows).
/// </summary>
public class AutorouteFieldSet : ContentFieldSet
{
    public const int MaxPathLength = 1024;

    public static readonly char[] InvalidCharactersForPath = ":?#[]@!$&'()*+,.;=<>\\|%".ToCharArray();

    public const string PathField = "Path";
    public const string SetHomepageField = "SetHomepage";
    public const string DisabledField = "Disabled";
    public const string RouteContainedItemsField = "RouteContainedItems";
    public const string AbsoluteField = "Absolute";

    [JsonIgnore]
    public string Path
    {
        get => this.Get<TextField>(PathField)?.Text;
        set => this.Alter<TextField>(PathField, text => text.Text = value);
    }

    /// <summary>
    /// Whether to make the content item the homepage once it's published.
    /// </summary>
    [JsonIgnore]
    public bool SetHomepage
    {
        get => this.Get<BooleanField>(SetHomepageField)?.Value ?? false;
        set => this.Alter<BooleanField>(SetHomepageField, flag => flag.Value = value);
    }

    /// <summary>
    /// Whether to disable autoroute generation for this content item.
    /// When disabled the path is no longer validated, or generated.
    /// </summary>
    [JsonIgnore]
    public bool Disabled
    {
        get => this.Get<BooleanField>(DisabledField)?.Value ?? false;
        set => this.Alter<BooleanField>(DisabledField, flag => flag.Value = value);
    }

    /// <summary>
    /// Whether to route content items contained within the content item.
    /// </summary>
    [JsonIgnore]
    public bool RouteContainedItems
    {
        get => this.Get<BooleanField>(RouteContainedItemsField)?.Value ?? false;
        set => this.Alter<BooleanField>(RouteContainedItemsField, flag => flag.Value = value);
    }

    /// <summary>
    /// When this content item is contained within another is the route relative to the container route.
    /// </summary>
    [JsonIgnore]
    public bool Absolute
    {
        get => this.Get<BooleanField>(AbsoluteField)?.Value ?? false;
        set => this.Alter<BooleanField>(AbsoluteField, flag => flag.Value = value);
    }
}
