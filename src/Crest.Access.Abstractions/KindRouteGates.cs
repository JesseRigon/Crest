using Crest.Kinds;

namespace Crest.Access;

/// <summary>
/// The route gates of a kind registry (docs/architecture.md › Kind-registry plumbing): the
/// registry's "All" page, and one gate per kind its own generic page serves. A kind whose
/// descriptor names its own route is its owner's page to gate, so it is left out unless
/// <see cref="Serves"/> says otherwise.
/// </summary>
public abstract class KindRoutePermissionProvider<TKind>(KindCatalog<TKind> catalog) : IRoutePermissionProvider
    where TKind : IKindDescriptor
{
    /// <summary>The registry's "All" page (base-relative).</summary>
    protected abstract string AllRoute { get; }

    /// <summary>The permission every route of the registry asks for.</summary>
    protected abstract string Permission { get; }

    /// <summary>The generic page route of a kind ("/Assets/{key}").</summary>
    protected abstract string GenericRoute(string key);

    /// <summary>Whether the registry's generic page serves this kind; by default, a kind with no route of its own.</summary>
    protected virtual bool Serves(TKind kind) => string.IsNullOrWhiteSpace(kind.Route);

    public IEnumerable<RoutePermission> GetRoutes()
    {
        yield return new(AllRoute, Permission);
        foreach (var kind in catalog.Kinds.Where(Serves))
        {
            yield return new(KindCatalog<TKind>.RouteOf(kind, GenericRoute), Permission);
        }
    }
}
