using Crest.Parties.Constants;
using Crest.Parties.PartyTypes;

namespace Crest.Parties.Services;

/// <summary>
/// The party-type registry: every <see cref="IPartyTypeProvider"/> the enabled features
/// registered, merged by key (first registration wins) and ordered. The menu, the route
/// gates and the All Parties page all read this one list, so a type a module adds shows
/// up in all three at once.
/// </summary>
public sealed class PartyTypeCatalog(IEnumerable<IPartyTypeProvider> providers)
{
    private IReadOnlyList<PartyTypeDescriptor>? _types;

    public IReadOnlyList<PartyTypeDescriptor> Types => _types ??= providers
        .SelectMany(provider => provider.GetTypes())
        .GroupBy(type => type.Key, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .OrderBy(type => type.Position)
        .ThenBy(type => type.DisplayName, StringComparer.Ordinal)
        .ToArray();

    public PartyTypeDescriptor? Find(string key) =>
        Types.FirstOrDefault(type => string.Equals(type.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Base-relative admin route: the descriptor's own, else Parties' page for the type.</summary>
    public static string RouteOf(PartyTypeDescriptor type) =>
        string.IsNullOrWhiteSpace(type.Route) ? PartiesConstants.Routes.PartyTypeAdmin(type.Key) : type.Route;

    /// <summary>Admin menu item id for the type; the served item's key differs (see PartyTypesController).</summary>
    public static string MenuItemIdOf(PartyTypeDescriptor type) => $"parties-{type.Key}";
}

/// <summary>Parties' own entries: the two base parties. Everything else is composed on them downstream.</summary>
public sealed class BasePartyTypeProvider : IPartyTypeProvider
{
    public IEnumerable<PartyTypeDescriptor> GetTypes() =>
    [
        new()
        {
            Key = PartiesConstants.PartyTypeKeys.Contacts,
            DisplayName = "Contacts",
            ContentType = PartiesConstants.ContentTypes.Person,
            Icon = "fa fa-address-card",
            Position = 10,
        },
        new()
        {
            Key = PartiesConstants.PartyTypeKeys.Organizations,
            DisplayName = "Organizations",
            ContentType = PartiesConstants.ContentTypes.Organization,
            Icon = "fa fa-building",
            Position = 20,
        },
    ];
}
