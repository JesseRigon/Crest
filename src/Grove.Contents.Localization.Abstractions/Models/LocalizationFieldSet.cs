using System.Text.Json.Serialization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Management;

namespace Grove.Contents.Localization.Models;

/// <summary>
/// An item's culture and the set of its translations. Its data is fields (<c>Localization.Culture</c>
/// and <c>Localization.LocalizationSet</c>, text fields); this class is the typed accessor over them.
/// Its behaviour (a new item's culture and set, a clone's own set, the localization entries) is the
/// module's system flows, and its culture a read projection (docs/content-engine.md › Behaviour is
/// workflows).
/// </summary>
public class LocalizationFieldSet : ContentFieldSet, ILocalizable
{
    public const string LocalizationSetField = "LocalizationSet";
    public const string CultureField = "Culture";

    [JsonIgnore]
    public string LocalizationSet
    {
        get => this.Get<TextField>(LocalizationSetField)?.Text;
        set => this.Alter<TextField>(LocalizationSetField, text => text.Text = value);
    }

    [JsonIgnore]
    public string Culture
    {
        get => this.Get<TextField>(CultureField)?.Text;
        set => this.Alter<TextField>(CultureField, text => text.Text = value);
    }
}
