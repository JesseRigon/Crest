#nullable enable

using System.Globalization;

namespace Crest.Localization;

/// <summary>
/// The tenant's stored edits of the admin client's strings (the translation store), layered over
/// the shipped PO translations by the client strings endpoint. A culture's parents come first, so
/// a region-specific edit wins.
/// </summary>
public interface IClientStringOverrides
{
    Task<IReadOnlyDictionary<string, string>> GetAsync(CultureInfo culture, string context);
}
