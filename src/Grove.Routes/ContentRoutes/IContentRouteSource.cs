namespace Grove.Routes.ContentRoutes;

/// <summary>
/// A kind of route content items carry (Autoroute paths, aliases): the route management page lists
/// each registered source's routes. A module registers its source from a startup that requires
/// its feature.
/// </summary>
public interface IContentRouteSource
{
    /// <summary>The source's key ("autoroute", "alias").</summary>
    string Name { get; }

    /// <summary>The invariant caption the page shows.</summary>
    string Caption { get; }

    /// <summary>The published routes, filtered by a path fragment, ordered by path, paged.</summary>
    Task<(IReadOnlyList<(string Path, string ContentItemId)> Items, int Total)> QueryAsync(string? search, int skip, int take, CancellationToken cancellationToken = default);
}
