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
    public IReadOnlyList<string>? Map(string permission, object resource, CallerContext caller)
    {
        string? contentType;
        var owned = false;

        switch (resource)
        {
            case ContentItem item:
                contentType = item.ContentType;
                owned = caller.UserId is not null && string.Equals(caller.UserId, item.Owner, StringComparison.Ordinal);
                break;
            case string typeName when !string.IsNullOrEmpty(typeName):
                contentType = typeName;
                break;
            case ContentTypeProbe probe:
                contentType = probe.ContentType;
                owned = probe.AsOwner;
                break;
            default:
                return null;
        }

        var effective = permission;
        if (owned && CommonPermissions.OwnerPermissionsByName.TryGetValue(permission, out var ownerVariation))
        {
            effective = ownerVariation.Name;
        }

        var candidates = new List<string>();
        if (ContentTypePermissionsHelper.PermissionTemplates.TryGetValue(effective, out var template) && !string.IsNullOrEmpty(contentType))
        {
            candidates.Add(ContentTypePermissionsHelper.CreateDynamicPermission(template, contentType).Name);
        }

        candidates.Add(effective);
        if (!string.Equals(effective, permission, StringComparison.Ordinal))
        {
            candidates.Add(permission);
        }

        return candidates;
    }
}

/// <summary>A resource standing in for "an item of this type, owned (or not) by the caller",
/// for decisions made without an item in hand (building a scope).</summary>
public sealed record ContentTypeProbe(string ContentType, bool AsOwner);
