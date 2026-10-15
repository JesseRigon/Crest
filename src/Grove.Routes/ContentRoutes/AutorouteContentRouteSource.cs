using Grove.Autoroute.Indexes;
using YesSql;
using YesSql.Services;

namespace Grove.Routes.ContentRoutes;

/// <summary>The published Autoroute paths (contained items' paths included).</summary>
public sealed class AutorouteContentRouteSource(ISession session) : IContentRouteSource
{
    public string Name => "autoroute";

    public string Caption => "Content paths";

    public async Task<(IReadOnlyList<(string Path, string ContentItemId)> Items, int Total)> QueryAsync(string? search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrWhiteSpace(search)
            ? session.QueryIndex<AutoroutePartIndex>(index => index.Published)
            : session.QueryIndex<AutoroutePartIndex>(index => index.Published && index.Path.Contains(search.Trim()));

        var total = await query.CountAsync();
        var rows = await query.OrderBy(index => index.Path).Skip(skip).Take(take).ListAsync();

        return ([.. rows.Select(row => (row.Path, string.IsNullOrEmpty(row.ContainedContentItemId) ? row.ContentItemId : row.ContainedContentItemId))], total);
    }
}
