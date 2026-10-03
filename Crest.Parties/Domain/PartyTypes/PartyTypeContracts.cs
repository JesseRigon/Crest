namespace Crest.Parties.PartyTypes;

/// <summary>
/// Where a party type's records live. Every party type but one is composed on the tenant's
/// base parties (a role content item pointing at a Person or Organization through its
/// Party picker - see the consuming module.s documentation); the exception is a catalogue
/// the global store owns, which a tenant references through a shim of its own and never
/// edits (tax authorities: plans/taxes.md › Tax authorities are parties).
/// </summary>
public static class PartyTypeKinds
{
    /// <summary>A tenant content type: the base parties themselves, or a role composed on them.</summary>
    public const string Tenant = "tenant";

    /// <summary>A global-store catalogue with a per-tenant shim. The owning module contributes the pane.</summary>
    public const string Global = "global";
}

/// <summary>
/// One entry in the party-type registry. Parties owns the registry and knows nothing about
/// the types it lists: each module that composes a party type registers a descriptor for
/// it (dependencies run upward - the roles are declared downstream of Parties).
/// </summary>
public sealed record PartyTypeDescriptor
{
    /// <summary>Stable slug, lower-case, used in routes and as the pane key: "customers".</summary>
    public required string Key { get; init; }

    /// <summary>Invariant caption literal, translated where displayed.</summary>
    public required string DisplayName { get; init; }

    public string Kind { get; init; } = PartyTypeKinds.Tenant;

    /// <summary>Tenant kind: the content type whose items the pane lists.</summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Base-relative admin route of the type's dedicated page. Tenant kinds default to
    /// "/Parties/{key}", served by Parties' own page; a global kind names its owner's page.
    /// </summary>
    public string? Route { get; init; }

    /// <summary>Icon class family + name as the admin menu takes it ("fa fa-users").</summary>
    public string? Icon { get; init; }

    public int Position { get; init; }
}

/// <summary>Implemented by any module that composes a party type. Registered scoped; Parties aggregates.</summary>
public interface IPartyTypeProvider
{
    IEnumerable<PartyTypeDescriptor> GetTypes();
}

/// <summary>Region keys the All Parties page opens for other modules (Crest's page-region seam).</summary>
public static class PartiesPageRegions
{
    /// <summary>
    /// The main pane for one party type. Context is the <see cref="PartyTypeModel"/>. Tenant
    /// kinds render Parties' generic list unless a contributor is registered; a global kind
    /// renders nothing but its contributors.
    /// </summary>
    public static string Pane(string typeKey) => $"Parties.Pane:{typeKey}";

    /// <summary>Below a selected record's summary in a tenant-kind pane. Context is the content item id.</summary>
    public static string Detail(string typeKey) => $"Parties.Detail:{typeKey}";
}

/// <summary>
/// What the client sees per party type: the descriptor plus its standing in the admin menu.
/// The pane list and the Parties menu are the same list, hidden by the same overlays -
/// the tenant's admin-menu layout and the user's own hidden-items preference - keyed by
/// the served menu item's key.
/// </summary>
public sealed record PartyTypeModel(
    string Key,
    string DisplayName,
    string Kind,
    string? ContentType,
    string Route,
    string? Icon,
    int Position,
    string? MenuItemKey,
    bool HiddenByUser);
