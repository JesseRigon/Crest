namespace Crest.Kinds;

/// <summary>
/// One entry in a kind registry: a party type, an asset kind, a transaction kind. The module
/// that owns the registry knows nothing about the kinds it lists; each module that ships a
/// kind registers a descriptor through the registry's provider interface.
/// </summary>
public interface IKindDescriptor
{
    /// <summary>Stable slug, lower-case, used in routes, menu item ids and pane keys: "customers".</summary>
    string Key { get; }

    /// <summary>Invariant caption literal, translated where displayed.</summary>
    string DisplayName { get; }

    /// <summary>The content type whose items the kind lists; null for a kind that is not one (a global catalogue).</summary>
    string? ContentType { get; }

    /// <summary>Base-relative admin route of the kind's own page; null when the registry's generic page serves it.</summary>
    string? Route { get; }

    /// <summary>Icon class family + name as the admin menu takes it ("fa fa-users").</summary>
    string? Icon { get; }

    int Position { get; }
}

/// <summary>
/// A kind registry: every descriptor the enabled features registered, merged by key (first
/// registration wins, keys compare ignoring case) and ordered by position, then display name.
/// The menu, the route gates, the "All" page and the per-kind panes all read this one list, so
/// a kind a module adds shows up in all of them at once. Registered scoped, like the providers
/// it reads.
/// </summary>
/// <typeparam name="TKind">The registry's descriptor.</typeparam>
public class KindCatalog<TKind>(Func<IEnumerable<TKind>> source) where TKind : IKindDescriptor
{
    private IReadOnlyList<TKind>? _kinds;

    public IReadOnlyList<TKind> Kinds => _kinds ??= source()
        .Where(Includes)
        .GroupBy(kind => kind.Key, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .OrderBy(kind => kind.Position)
        .ThenBy(kind => kind.DisplayName, StringComparer.Ordinal)
        .ToArray();

    /// <summary>The kind with this key, ignoring case; null when none is registered.</summary>
    public TKind? Find(string? key) =>
        key is null ? default : Kinds.FirstOrDefault(kind => string.Equals(kind.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>The kind whose records are items of this content type; null when none is.</summary>
    public TKind? ForContentType(string? contentType) =>
        contentType is null ? default : Kinds.FirstOrDefault(kind => string.Equals(kind.ContentType, contentType, StringComparison.Ordinal));

    /// <summary>A registry's own filter over what the providers registered (a known class, a valid shape); everything by default.</summary>
    protected virtual bool Includes(TKind kind) => true;

    /// <summary>Base-relative admin route: the descriptor's own, else <paramref name="genericRoute"/> (the registry's page for the key).</summary>
    public static string RouteOf(TKind kind, Func<string, string> genericRoute) =>
        string.IsNullOrWhiteSpace(kind.Route) ? genericRoute(kind.Key) : kind.Route;
}
