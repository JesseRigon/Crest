#nullable enable
namespace Crest.Media.Files;

/// <summary>
/// The placement graph (ruling F4). A folder's children belong to its content, so every hard
/// placement of a folder shows the same children and the graph is a DAG: a placement's closure
/// holds every placement of every folder above it, through any path, with its shortest depth.
/// The closure is baked into the placement documents and recalculated only for the placements
/// a create, move or removal touches and everything below them; reads never walk the tree.
/// Callers hold the tenant's graph lock while they change placements.
/// </summary>
public sealed class MediaGraph(IMediaObjectStore store) : IMediaGraph
{
    public async Task<IReadOnlyList<string>> AncestorsAsync(string placementId, CancellationToken cancellationToken = default)
    {
        var placement = await store.GetPlacementAsync(placementId);
        return placement is null
            ? []
            : [.. placement.Ancestors.OrderBy(ancestor => ancestor.Depth).ThenBy(ancestor => ancestor.PlacementId, StringComparer.Ordinal).Select(ancestor => ancestor.PlacementId)];
    }

    public async Task<bool> WouldLoopAsync(string contentId, string parentPlacementId, CancellationToken cancellationToken = default)
    {
        var parent = await store.GetPlacementAsync(parentPlacementId);
        if (parent?.Placement.LinkTargetPlacementId is { } target)
        {
            parent = await store.GetPlacementAsync(target);
        }

        if (parent is null)
        {
            return false;
        }

        if (parent.Placement.ContentId == contentId)
        {
            return true;
        }

        // Inside a folder is inside every placement of it: the destination's content may sit
        // below the moved folder through a placement other than the one asked about.
        foreach (var holder in await store.GetPlacementsOfContentAsync(parent.Placement.ContentId))
        {
            if (await store.HasAncestorContentAsync(holder.Placement.Id, contentId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Recalculates the closure (and drive) of <paramref name="changed"/> and of everything below
    /// them, parents before children, and saves what changed. Removed placements must already be
    /// deleted from the store.
    /// </summary>
    public async Task RecalculateAsync(IEnumerable<MediaPlacementDocument> changed)
    {
        // Everything below the changed placements: the children of every hard placement of each
        // folder reached, breadth first. Soft links are leaves.
        var affected = new Dictionary<string, MediaPlacementDocument>(StringComparer.Ordinal);
        var queue = new Queue<MediaPlacementDocument>(changed);
        var expanded = new HashSet<string>(StringComparer.Ordinal);
        while (queue.Count > 0)
        {
            var document = queue.Dequeue();
            if (!affected.TryAdd(document.Placement.Id, document) || document.Placement.IsSoftLink || !expanded.Add(document.Placement.ContentId))
            {
                continue;
            }

            var placements = await store.GetPlacementsOfContentAsync(document.Placement.ContentId);
            foreach (var child in await store.GetChildrenAsync([.. placements.Select(placement => placement.Placement.Id)]))
            {
                queue.Enqueue(affected.GetValueOrDefault(child.Placement.Id) ?? child);
            }
        }

        var done = new Dictionary<string, List<MediaAncestor>>(StringComparer.Ordinal);
        foreach (var document in affected.Values)
        {
            await ClosureAsync(document, affected, done, 0);
        }

        foreach (var document in affected.Values)
        {
            await store.SaveAsync(document);
        }
    }

    private const int MaxDepth = 256;

    private async Task<List<MediaAncestor>> ClosureAsync(MediaPlacementDocument document, Dictionary<string, MediaPlacementDocument> affected, Dictionary<string, List<MediaAncestor>> done, int depth)
    {
        if (!affected.ContainsKey(document.Placement.Id))
        {
            return document.Ancestors;
        }

        if (done.TryGetValue(document.Placement.Id, out var known))
        {
            return known;
        }

        if (depth > MaxDepth)
        {
            throw new MediaObjectConflictException("The folder tree is too deep.");
        }

        var ancestors = new Dictionary<string, MediaAncestor>(StringComparer.Ordinal);
        if (document.Placement.ParentId is { } parentId)
        {
            var parent = affected.GetValueOrDefault(parentId) ?? await store.GetPlacementAsync(parentId)
                ?? throw new MediaObjectConflictException("A placement's folder no longer exists.");

            // Every hard placement of the folder's content holds this child.
            foreach (var holder in await store.GetPlacementsOfContentAsync(parent.Placement.ContentId))
            {
                var placement = affected.GetValueOrDefault(holder.Placement.Id) ?? holder;
                Add(ancestors, new MediaAncestor(placement.Placement.Id, placement.Placement.ContentId, 1));
                foreach (var above in await ClosureAsync(placement, affected, done, depth + 1))
                {
                    Add(ancestors, above with { Depth = above.Depth + 1 });
                }
            }

            document.Placement.DriveId = parent.Placement.DriveId;
        }
        else
        {
            document.Placement.DriveId = document.Placement.Id;
        }

        var closure = ancestors.Values
            .OrderBy(ancestor => ancestor.Depth)
            .ThenBy(ancestor => ancestor.PlacementId, StringComparer.Ordinal)
            .ToList();
        document.Ancestors = closure;
        done[document.Placement.Id] = closure;
        return closure;
    }

    private static void Add(Dictionary<string, MediaAncestor> ancestors, MediaAncestor ancestor)
    {
        if (!ancestors.TryGetValue(ancestor.PlacementId, out var existing) || existing.Depth > ancestor.Depth)
        {
            ancestors[ancestor.PlacementId] = ancestor;
        }
    }
}
