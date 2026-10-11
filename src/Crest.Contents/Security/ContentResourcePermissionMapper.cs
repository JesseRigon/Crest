using Crest.Access;
using Crest.ContentManagement;

namespace Crest.Contents.Security;

/// <summary>
/// Maps a content permission asked against a content item or a content type name to the
/// permissions that decide it: the owner variation when the caller owns the item, the
/// per-type dynamic permission, and the base permission itself (any of them grants).
/// </summary>
public sealed class ContentResourcePermissionMapper : IResourcePermissionMapper
{
    private static bool IsContentPermission(string permission) => ContentPermissionNames.Matches(permission);

    public ValueTask<IReadOnlyList<PermissionCandidate>?> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
    {
        string? contentType;
        var owned = false;

        switch (resource)
        {
            case ContentItem item:
                contentType = item.ContentType;
                owned = caller.UserId is not null && string.Equals(caller.UserId, item.Owner, StringComparison.Ordinal);
                break;
            // A bare string is a content type name only for a content permission: other
            // modules ask string resources too (a media path, a settings group id).
            case string typeName when !string.IsNullOrEmpty(typeName) && IsContentPermission(permission):
                contentType = typeName;
                break;
            case ContentTypeProbe probe:
                contentType = probe.ContentType;
                owned = probe.AsOwner;
                break;
            default:
                return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(null);
        }

        var effective = permission;
        if (owned && CommonPermissions.OwnerPermissionsByName.TryGetValue(permission, out var ownerVariation))
        {
            effective = ownerVariation.Name;
        }

        var candidates = new List<PermissionCandidate>();
        if (ContentTypePermissionsHelper.PermissionTemplates.TryGetValue(effective, out var template) && !string.IsNullOrEmpty(contentType))
        {
            candidates.Add(ContentTypePermissionsHelper.CreateDynamicPermission(template, contentType).Name);
        }

        candidates.Add(effective);
        if (!string.Equals(effective, permission, StringComparison.Ordinal))
        {
            candidates.Add(permission);
        }

        return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>?>(candidates);
    }
}

/// <summary>Whether a permission is one of the content permissions with a per-type template.</summary>
file static class ContentPermissionNames
{
    public static bool Matches(string permission) =>
        ContentTypePermissionsHelper.PermissionTemplates.ContainsKey(permission) || CommonPermissions.OwnerPermissionsByName.ContainsKey(permission);
}

/// <summary>A resource standing in for "an item of this type, owned (or not) by the caller",
/// for decisions made without an item in hand (building a scope).</summary>
public sealed record ContentTypeProbe(string ContentType, bool AsOwner);
