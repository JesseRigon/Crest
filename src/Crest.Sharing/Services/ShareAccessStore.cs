using Crest.Sharing.Indexes;
using YesSql;
using YesSql.Services;

namespace Crest.Sharing.Services;

/// <summary>Where the access index documents and the pending recalculations live.</summary>
public interface IShareAccessStore
{
    Task<ShareAccessDocument?> GetAsync(ShareableRef target);

    Task SaveAsync(ShareAccessDocument document);

    Task DeleteAsync(ShareableRef target);

    /// <summary>Up to <paramref name="max"/> objects whose next boundary is at or before
    /// <paramref name="utc"/> (the time registry), earliest first.</summary>
    Task<IReadOnlyList<ShareableRef>> DueAsync(DateTime utc, int max);

    /// <summary>A page of every calculated object, by document order, after <paramref name="afterId"/>
    /// (a tenant-wide change recalculates them all). Returns the page and the cursor for the next.</summary>
    Task<(IReadOnlyList<ShareableRef> Page, long Cursor)> PageAsync(long afterId, int max);

    /// <summary>The objects with a row granting at least Viewer to one of
    /// <paramref name="principals"/>, decided on the object itself (not inherited) and active
    /// at <paramref name="utc"/>.</summary>
    Task<IReadOnlyList<ShareableRef>> FindGrantedAsync(IReadOnlyCollection<SharePrincipal> principals, DateTime utc);

    Task QueueAsync(ShareableRef subtree, DateTime utc);

    Task<IReadOnlyList<ShareRecalculation>> QueuedAsync(int max);

    Task DequeueAsync(ShareRecalculation recalculation);

    /// <summary>Writes what was saved so far (a batch of a long recalculation).</summary>
    Task FlushAsync();

    /// <summary>Commits (the background job, which has no request to commit with).</summary>
    Task CommitAsync();
}

public sealed class YesSqlShareAccessStore(ISession session) : IShareAccessStore
{
    private readonly Dictionary<ShareableRef, ShareAccessDocument?> _read = [];

    public async Task<ShareAccessDocument?> GetAsync(ShareableRef target)
    {
        if (!_read.TryGetValue(target, out var document))
        {
            document = await session.Query<ShareAccessDocument, ShareAccessObjectIndex>(index => index.Kind == target.Kind && index.ObjectId == target.Id).FirstOrDefaultAsync();
            _read[target] = document;
        }

        return document;
    }

    public async Task SaveAsync(ShareAccessDocument document)
    {
        if (await GetAsync(document.Ref) is { } existing && !ReferenceEquals(existing, document))
        {
            // One document per object: the new calculation replaces the stored one in place.
            existing.IsPublic = document.IsPublic;
            existing.IsInactive = document.IsInactive;
            existing.NextBoundaryUtc = document.NextBoundaryUtc;
            existing.CalculatedUtc = document.CalculatedUtc;
            existing.Rows = document.Rows;
            existing.Shadows = document.Shadows;
            document = existing;
        }

        await session.SaveAsync(document);
        _read[document.Ref] = document;
    }

    public async Task DeleteAsync(ShareableRef target)
    {
        if (await GetAsync(target) is { } existing)
        {
            session.Delete(existing);
        }

        _read[target] = null;
    }

    public async Task<IReadOnlyList<ShareableRef>> DueAsync(DateTime utc, int max)
    {
        var due = await session.QueryIndex<ShareAccessObjectIndex>(index => index.NextBoundaryUtc != null && index.NextBoundaryUtc <= utc)
            .OrderBy(index => index.NextBoundaryUtc)
            .Take(max)
            .ListAsync();
        return [.. due.Select(index => new ShareableRef(index.Kind, index.ObjectId))];
    }

    public async Task<(IReadOnlyList<ShareableRef> Page, long Cursor)> PageAsync(long afterId, int max)
    {
        var page = (await session.QueryIndex<ShareAccessObjectIndex>(index => index.Id > afterId)
            .OrderBy(index => index.Id)
            .Take(max)
            .ListAsync()).ToList();
        return ([.. page.Select(index => new ShareableRef(index.Kind, index.ObjectId))], page.Count == 0 ? afterId : page[^1].Id);
    }

    public async Task<IReadOnlyList<ShareableRef>> FindGrantedAsync(IReadOnlyCollection<SharePrincipal> principals, DateTime utc)
    {
        var found = new HashSet<ShareableRef>();
        foreach (var group in principals.GroupBy(principal => principal.Kind))
        {
            var kind = (int)group.Key;
            var ids = group.Select(ShareAccessIndexProvider.IndexedId).Distinct().ToArray();
            var rows = await session.QueryIndex<ShareAccessIndex>(index =>
                    index.PrincipalKind == kind && index.PrincipalId.IsIn(ids) && index.IsDirect && index.Level >= (int)AccessLevel.Viewer &&
                    (index.StartsUtc == null || index.StartsUtc <= utc) && (index.EndsUtc == null || index.EndsUtc > utc))
                .ListAsync();
            found.UnionWith(rows.Select(row => new ShareableRef(row.Kind, row.ObjectId)));
        }

        return [.. found];
    }

    public async Task QueueAsync(ShareableRef subtree, DateTime utc)
    {
        var queued = await session.Query<ShareRecalculation, ShareRecalculationIndex>(index => index.Kind == subtree.Kind && index.ObjectId == subtree.Id).FirstOrDefaultAsync();
        if (queued is null)
        {
            await session.SaveAsync(new ShareRecalculation { Kind = subtree.Kind, ObjectId = subtree.Id, RequestedUtc = utc });
        }
    }

    public async Task<IReadOnlyList<ShareRecalculation>> QueuedAsync(int max) =>
        [.. await session.Query<ShareRecalculation, ShareRecalculationIndex>().OrderBy(index => index.RequestedUtc).Take(max).ListAsync()];

    public Task DequeueAsync(ShareRecalculation recalculation)
    {
        session.Delete(recalculation);
        return Task.CompletedTask;
    }

    public Task FlushAsync() => session.FlushAsync();

    public Task CommitAsync() => session.SaveChangesAsync();
}
