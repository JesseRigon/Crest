#nullable enable

using System.Globalization;
using Crest.Validation;

namespace Crest.Localization.Api;

public sealed record LocalizationModel(
    string DefaultCulture,
    [property: NotEmpty] string[] SupportedCultures,
    bool FallBackToParentCulture,
    // Rung 3 of the client resolution chain (docs/localization.md's "Resolution
    // architecture") - a tenant-level default distinct from DefaultCulture above, only
    // consulted by the client when the current route is under the admin path prefix. Null
    // means "no admin-specific override" - the client falls through to rung 4/5 as if this
    // setting didn't exist.
    string? AdminDefaultCulture,
    Culture[] AvailableCultures);

public sealed record Culture(string Value, string Label, string NativeLabel)
{
    public static Culture From(CultureInfo culture) => new(
        culture.Name,
        string.IsNullOrWhiteSpace(culture.DisplayName) ? culture.Name : culture.DisplayName,
        string.IsNullOrWhiteSpace(culture.NativeName) ? culture.DisplayName : culture.NativeName);
}
