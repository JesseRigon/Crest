using Grove.Access.Sharing.Indexes;
using YesSql;
using YesSql.Services;

namespace Grove.Access.Sharing.Services;

/// <summary>Where the access index documents and the pending recalculations live.</summary>
public interface IShareAccessStore
{
    /// <summary>The object's document for its primary path.</summary>
    Task<ShareAccessDocument?> GetAsync(ShareableRef target);

    /// <summary>The object's documents, one per path to the root (multi-path inheritance, F3).</summary>
    Task<IReadOnlyList<ShareAccessDocument>> GetPathsAsync(ShareableRef target);

    /// <summary>Saves one path's document, replacing the stored one for the same path.</summary>
    Task SaveAsync(ShareAccessDocument document);

    /// <summary>Saves an object's documents, one per path, and removes those of paths it no
    /// longer has.</summary>
    Task SavePathsAsync(ShareableRef target, IReadOnlyList<ShareAccessDocument> documents);

    /// <summary>Deletes every path's document of the object.</summary>
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
    private readonly Dictionary<ShareableRef, List<ShareAccessDocument>> _read = [];

    public async Task<ShareAccessDocument?> GetAsync(ShareableRef target) =>
        (await LoadAsync(target)).FirstOrDefault(document => document.PathKey.Length == 0);

    public async Task<IReadOnlyList<ShareAccessDocument>> GetPathsAsync(ShareableRef target) => [.. await LoadAsync(target)];

    private async Task<List<ShareAccessDocument>> LoadAsync(ShareableRef target)
    {
        if (!_read.TryGetValue(target, out var documents))
        {
            documents = [.. await session.Query<ShareAccessDocument, ShareAccessObjectIndex>(index => index.Kind == target.Kind && index.ObjectId == target.Id).ListAsync()];
            _read[target] = documents;
        }

        return documents;
    }

    public async Task SaveAsync(ShareAccessDocument document)
    {
        var documents = await LoadAsync(document.Ref);
        var existing = documents.FirstOrDefault(candidate => candidate.PathKey == document.PathKey);
        if (existing is not null && !ReferenceEquals(existing, document))
        {
            // One document per object and path: the new calculation replaces the stored one in place.
            existing.IsPublic = document.IsPublic;
            existing.IsShared = document.IsShared;
            existing.IsInactive = document.IsInactive;
            existing.NextBoundaryUtc = document.NextBoundaryUtc;
            existing.CalculatedUtc = document.CalculatedUtc;
            existing.Rows = document.Rows;
            existing.Shadows = document.Shadows;
            document = existing;
        }
        else if (existing is null)
        {
            documents.Add(document);
        }

        await session.SaveAsync(document);
    }

    public async Task SavePathsAsync(ShareableRef target, IReadOnlyList<ShareAccessDocument> documents)
    {
        foreach (var document in documents)
        {
            await SaveAsync(document);
        }

        var keep = documents.Select(document => document.PathKey).ToHashSet(StringComparer.Ordinal);
        var stored = await LoadAsync(target);
        foreach (var gone in stored.Where(document => !keep.Contains(document.PathKey)).ToList())
        {
            session.Delete(gone);
            stored.Remove(gone);
        }
    }

    public async Task DeleteAsync(ShareableRef target)
    {
        foreach (var existing in await LoadAsync(target))
        {
            session.Delete(existing);
        }

        _read[target] = [];
    }

    public async Task<IReadOnlyList<ShareableRef>> DueAsync(DateTime utc, int max)
    {
        var due = await session.QueryIndex<ShareAccessObjectIndex>(index => index.NextBoundaryUtc != null && index.NextBoundaryUtc <= utc)
            .OrderBy(index => index.NextBoundaryUtc)
            .Take(max)
            .ListAsync();
        return [.. due.Select(index => new ShareableRef(index.Kind, index.ObjectId)).Distinct()];
    }

    public async Task<(IReadOnlyList<ShareableRef> Page, long Cursor)> PageAsync(long afterId, int max)
    {
        var page = (await session.QueryIndex<ShareAccessObjectIndex>(index => index.Id > afterId)
            .OrderBy(index => index.Id)
            .Take(max)
            .ListAsync()).ToList();
        return ([.. page.Select(index => new ShareableRef(index.Kind, index.ObjectId)).Distinct()], page.Count == 0 ? afterId : page[^1].Id);
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

    public async Task CommitAsync()
    {
        await session.SaveChangesAsync();

        // A commit ends the session's tracking (YesSql clears its identity map): documents read
        // before it would be saved again as new ones, or fail to delete.
        _read.Clear();
    }
}
