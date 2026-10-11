using Crest.Access;
using Crest.Indexing.Models;

namespace Crest.Indexing.Handlers;

/// <summary>
/// <c>QuerySearchIndex</c> asked against an index profile is also granted by that index's
/// own dynamic permission.
/// </summary>
public sealed class IndexingPermissionMapper : IResourcePermissionMapper
{
    public ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (resource is not IndexProfile indexProfile || permission != IndexingPermissions.QuerySearchIndex.Name)
        {
            return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(null);
        }

        IReadOnlyList<PermissionCandidate> candidates = [IndexingPermissions.CreateDynamicPermission(indexProfile).Name, permission];
        return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(candidates);
    }
}
