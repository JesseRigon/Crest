using Grove.Sharing;

namespace Grove.Media.Sharing;

/// <summary>
/// Media as a shareable kind (docs/sharing.md, docs/media.md): while the Sharing feature is on,
/// sharing decides who may view and manage media, through the access gate, in place of the media
/// folder permissions. Until file objects exist (media.md § 3) an object is a media path: the
/// media library's root ("") is the one root - its Admins administer all media - every folder
/// and file below it inherits down the path, and rules test a file's extension. Paths are the
/// resources the media surfaces already ask the decision about (the media API, secure media
/// serving), so nothing else changes in Media.
/// </summary>
public sealed class MediaShareableKind : IShareableKind
{
    public const string KindName = "media";

    public string Name => KindName;

    public bool TryIdentify(object resource, out string id)
    {
        // A media request asks with the path; other string resources (settings groups, content
        // types) are left alone because LevelFor maps only the media permissions.
        if (resource is string path)
        {
            id = Normalize(path);
            return true;
        }

        id = string.Empty;
        return false;
    }

    public Task<ShareableObject?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = Normalize(id);
        var facts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var extension = Path.GetExtension(path).TrimStart('.');
        if (extension.Length > 0)
        {
            facts["extension"] = [extension.ToLowerInvariant()];
        }

        facts["path"] = [path];
        return Task.FromResult<ShareableObject?>(new ShareableObject
        {
            Ref = new ShareableRef(KindName, path),
            Parent = path.Length == 0 ? null : new ShareableRef(KindName, ParentOf(path)),
            Facts = facts,
        });
    }

    /// <summary>The media permissions by access level: viewing is Viewer; managing (create,
    /// update, delete) is Manager. Anything else is not about media objects.</summary>
    public AccessLevel? LevelFor(string permission) => permission switch
    {
        _ when permission == MediaPermissions.ViewMedia.Name ||
               permission == MediaPermissions.ViewRootMedia.Name ||
               permission == MediaPermissions.ViewOwnMedia.Name ||
               permission == MediaPermissions.ViewOthersMedia.Name => AccessLevel.Viewer,
        _ when permission == MediaPermissions.ManageMediaFolder.Name ||
               permission == MediaPermissions.ManageMedia.Name ||
               permission == MediaPermissions.ManageOwnMedia.Name ||
               permission == MediaPermissions.ManageOthersMedia.Name ||
               permission == MediaPermissions.ManageAttachedMediaFieldsFolder.Name => AccessLevel.Manager,
        _ => null,
    };

    public static string Normalize(string path) => path.Replace('\\', '/').Trim('/');

    private static string ParentOf(string path)
    {
        var index = path.LastIndexOf('/');
        return index < 0 ? string.Empty : path[..index];
    }
}
