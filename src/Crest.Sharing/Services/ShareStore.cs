using Crest.Sharing.Indexes;
using YesSql;
using YesSql.Services;

namespace Crest.Sharing.Services;

/// <summary>Where the share documents live. Scoped: a document read once per request.</summary>
public interface IShareStore
{
    Task<ShareDocument?> GetAsync(ShareableRef target);

    Task SaveAsync(ShareDocument document);

    /// <summary>The objects with an explicit grant (not a block) to any of <paramref name="principals"/>.</summary>
    Task<IReadOnlyList<ShareableRef>> FindGrantedAsync(IReadOnlyCollection<SharePrincipal> principals);
}

public sealed class YesSqlShareStore(ISession session) : IShareStore
{
    private readonly Dictionary<ShareableRef, ShareDocument?> _read = [];

    public async Task<ShareDocument?> GetAsync(ShareableRef target)
    {
        if (!_read.TryGetValue(target, out var document))
        {
            document = await session.Query<ShareDocument, ShareDocumentIndex>(index => index.Kind == target.Kind && index.ObjectId == target.Id).FirstOrDefaultAsync();
            _read[target] = document;
        }

        return document;
    }

    public async Task SaveAsync(ShareDocument document)
    {
        await session.SaveAsync(document);
        _read[document.Ref] = document;
    }

    public async Task<IReadOnlyList<ShareableRef>> FindGrantedAsync(IReadOnlyCollection<SharePrincipal> principals)
    {
        var found = new HashSet<ShareableRef>();
        foreach (var group in principals.GroupBy(principal => principal.Kind))
        {
            var kind = (int)group.Key;
            var ids = group.Select(principal => principal.Id ?? string.Empty).ToArray();
            var rows = await session.QueryIndex<ShareEntryIndex>(index => index.PrincipalKind == kind && index.PrincipalId.IsIn(ids) && !index.IsBlock).ListAsync();
            found.UnionWith(rows.Select(row => new ShareableRef(row.Kind, row.ObjectId)));
        }

        return [.. found];
    }
}
