namespace Crest.Parties.ViewModels;

/// <summary>A party as list consumers see it. Email and Phone are DERIVED from the
/// party's contact points (see <see cref="PartyContactRules"/>), not stored fields.</summary>
public sealed record PartyModel(
    string ContentItemId,
    string ContentType,
    string DisplayText,
    string? Email,
    string? Phone);

/// <summary>One way of reaching a party. <paramref name="Kind"/> and
/// <paramref name="PhoneCountry"/> are option KEYS (the stored content holds option
/// ids; the API speaks keys, which is what code compares).</summary>
public sealed record ContactPointModel(
    string Id,
    string? Kind,
    string Value,
    string? Label,
    bool Preferred,
    string? PhoneCountry);

public sealed record ContactPointWriteModel(
    string Kind,
    string Value,
    string? Label = null,
    bool Preferred = false,
    string? PhoneCountry = null);

/// <summary>
/// <paramref name="Kind"/> is an option key. <paramref name="Country"/> is the level-1
/// geo node id (ISO 3166-1 alpha-2). <paramref name="LevelNodeIds"/> is the rest of the
/// address's geo stack, one node id per level the country's addressing map uses
/// (plans/regions-and-locations.md); <paramref name="Locality"/> is the free-text city /
/// post town / commune.
/// </summary>
public sealed record AddressModel(
    string Id,
    string? Kind,
    string? Line1,
    string? Line2,
    string? Locality,
    string? PostalCode,
    string? Country,
    IReadOnlyDictionary<int, string> LevelNodeIds,
    IReadOnlyList<string> BoundaryNodeIds,
    double? Latitude,
    double? Longitude,
    bool Preferred);

public sealed record AddressWriteModel(
    string Kind,
    string? Line1 = null,
    string? Line2 = null,
    string? Locality = null,
    string? PostalCode = null,
    string? Country = null,
    IReadOnlyDictionary<int, string?>? LevelNodeIds = null,
    bool Preferred = false,
    double? Latitude = null,
    double? Longitude = null);

public sealed record PartyContactsModel(
    IReadOnlyList<ContactPointModel> ContactPoints,
    IReadOnlyList<AddressModel> Addresses);

/// <summary>A position a person holds in an organization. A person may hold many,
/// in many organizations; <paramref name="Primary"/> marks the one to show first.</summary>
public sealed record PositionModel(
    string Id,
    string OrganizationId,
    string? OrganizationName,
    string? Title,
    string? Department,
    bool Primary);

public sealed record PositionWriteModel(
    string OrganizationId,
    string? Title = null,
    string? Department = null,
    bool Primary = false);

/// <summary>The organization-side view of a position: who holds it.</summary>
public sealed record OrganizationPersonModel(
    string PersonId,
    string PersonName,
    string PositionId,
    string? Title,
    string? Department,
    bool Primary);

/// <summary>
/// Pure selection rules over a party's contact data. Server-free so the same answer
/// is computed wherever the data is (server mapper, Blazor page) and testable alone.
/// </summary>
public static class PartyContactRules
{
    /// <summary>
    /// The contact point to show as "the" one of a family of kinds: a preferred entry
    /// wins; otherwise the first entry of the earliest-listed kind (so for
    /// [phone, mobile] a landline outranks a mobile unless the mobile is preferred).
    /// </summary>
    public static ContactPointModel? Primary(IEnumerable<ContactPointModel> contactPoints, params string[] kinds)
    {
        var candidates = contactPoints
            .Where(point => point.Kind is not null && kinds.Contains(point.Kind, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        return candidates.FirstOrDefault(point => point.Preferred)
            ?? candidates
                .OrderBy(point => Array.FindIndex(kinds, kind => string.Equals(kind, point.Kind, StringComparison.OrdinalIgnoreCase)))
                .First();
    }

    /// <summary>The address of a kind to use: preferred first, else the first of that
    /// kind in stored order.</summary>
    public static AddressModel? Primary(IEnumerable<AddressModel> addresses, string kind)
    {
        var candidates = addresses
            .Where(address => string.Equals(address.Kind, kind, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return candidates.FirstOrDefault(address => address.Preferred) ?? candidates.FirstOrDefault();
    }

    /// <summary>A single-line rendering for lists and snapshots; null when every part
    /// is blank.</summary>
    public static string? FormatSingleLine(AddressModel address)
    {
        var parts = new[] { address.Line1, address.Line2, address.Locality, address.PostalCode, address.Country }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim());

        var line = string.Join(", ", parts);
        return line.Length == 0 ? null : line;
    }
}

/// <summary>A tenant Location. <paramref name="IsPrimary"/> marks the tax "home".</summary>
public sealed record LocationModel(
    string Id,
    string DisplayText,
    bool IsPrimary,
    decimal? FloorArea,
    int? Storeys,
    IReadOnlyList<AddressModel> Addresses,
    IReadOnlyList<LocationPointModel> Points);

/// <summary>A labelled point at a Location with its own geo stack and coordinates.</summary>
public sealed record LocationPointModel(
    string Id,
    string Label,
    string? Country,
    IReadOnlyDictionary<int, string> LevelNodeIds,
    double? Latitude,
    double? Longitude,
    decimal? FloorArea,
    int? Storey);
