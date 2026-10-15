using Grove.Alias.Indexes;
using YesSql;
using YesSql.Services;

namespace Grove.Routes.ContentRoutes;

/// <summary>The published aliases.</summary>
public sealed class AliasContentRouteSource(ISession session) : IContentRouteSource
{
    public string Name => "alias";

    public string Caption => "Aliases";

    public async Task<(IReadOnlyList<(string Path, string ContentItemId)> Items, int Total)> QueryAsync(string? search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrWhiteSpace(search)
            ? session.QueryIndex<AliasPartIndex>(index => index.Published)
            : session.QueryIndex<AliasPartIndex>(index => index.Published && index.Alias.Contains(search.Trim()));

        var total = await query.CountAsync();
        var rows = await query.OrderBy(index => index.Alias).Skip(skip).Take(take).ListAsync();

        return ([.. rows.Select(row => (row.Alias, row.ContentItemId))], total);
    }
}
