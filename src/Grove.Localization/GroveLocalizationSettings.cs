#nullable enable

namespace Grove.Localization;

// Grove-owned, separate from Grove's own LocalizationSettings - AdminDefaultCulture
// is a Grove concept the platform had no equivalent for.
public sealed class GroveLocalizationSettings
{
    public string? AdminDefaultCulture { get; set; }
}
