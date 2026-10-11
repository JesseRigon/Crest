using Crest.Access;
using Crest.ContentManagement;

namespace Crest.ContentLocalization.Security;

/// <summary>
/// <c>LocalizeContent</c> asked against a content item the caller owns is also granted by
/// <c>LocalizeOwnContent</c>.
/// </summary>
public sealed class LocalizeContentPermissionMapper : IResourcePermissionMapper
{
    public ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        if (resource is not ContentItem item || permission != ContentLocalizationPermissions.LocalizeContent.Name)
        {
            return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(null);
        }

        var owned = caller.UserId is not null && string.Equals(caller.UserId, item.Owner, StringComparison.Ordinal);
        IReadOnlyList<PermissionCandidate> candidates = owned
            ? [ContentLocalizationPermissions.LocalizeOwnContent.Name, permission]
            : [permission];
        return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(candidates);
    }
}
