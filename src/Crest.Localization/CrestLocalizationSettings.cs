#nullable enable

namespace Crest.Localization;

// Crest-owned, separate from Crest's own LocalizationSettings - AdminDefaultCulture
// is a Crest concept the platform had no equivalent for.
public sealed class CrestLocalizationSettings
{
    public string? AdminDefaultCulture { get; set; }
}
