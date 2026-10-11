#nullable enable
using Crest.Media.Files;
using Crest.Sharing;

namespace Crest.Media.Sharing;

/// <summary>
/// File objects as a shareable kind (docs/media.md § 3, docs/sharing.md): an object is a
/// placement, so each placement of a hard-linked file has its own sharing, inherited down its
/// own path (ruling F1). The parent is the placement's folder; the root is its drive, where
/// Drive Admin is granted. The owner is the user for what is in a personal drive (everything
/// there is theirs, requirement 14) and nobody for what a system account owns. Everything in
/// an inactive drive is inactive. A soft link is identified as its target, so it opens the
/// target with the target's access and grants nothing of its own.
/// </summary>
public sealed class MediaFileShareableKind(IMediaObjectStore store) : IShareableKind, IShareableTree, IShareableScopeTarget, IShareableRoots, IShareableAdminAssignment, IShareableAdminEligibility
{
    /// <summary>Holders of <c>ManageSharedDrives</c> assign and remove Drive Admins without being
    /// one (requirement 31).</summary>
    public string AdminAssignmentPermission => MediaPermissions.ManageSharedDrives.Name;

    /// <summary>Shared drives only. Never a personal drive: an administrator reaches one only
    /// through the separate, noticed and logged access of docs/media.md § 7. Not an
    /// organization drive either: its admins are the organization's own (requirement 17), and
    /// the tenant's administrators can still add others.</summary>
    public bool AppliesTo(ShareableObject root) =>
        root.Facts.TryGetValue(Facts.DriveType, out var type) && type.Contains("shared", StringComparer.OrdinalIgnoreCase);

    /// <summary>On a personal drive only its owner holds Admin (requirement 19).</summary>
    public bool MayHoldAdmin(ShareableObject root, SharePrincipal principal) =>
        !(root.Facts.TryGetValue(Facts.DriveType, out var type) && type.Contains("personal", StringComparer.OrdinalIgnoreCase)) ||
        (principal.Kind == SharePrincipalKind.User && principal.Id == root.OwnerUserId);

    /// <summary>Every drive, for rebuilding the access index from nothing.</summary>
    public async Task<IReadOnlyList<ShareableRef>> RootsAsync(CancellationToken cancellationToken = default) =>
        [.. (await store.GetDrivesAsync()).Select(drive => new ShareableRef(KindName, drive.Placement.Id))];

    public const string KindName = FileShareableKind.Name;

    /// <summary>Lists of placements are filtered on the placement index, by the placement whose
    /// access decides each row (a soft link's target).</summary>
    public ShareScopeTarget ScopeTarget { get; } = new(nameof(MediaPlacementIndex), nameof(MediaPlacementIndex.AccessId));

    /// <summary>The facts rules can test (requirement 56).</summary>
    public static class Facts
    {
        public const string Extension = "extension";

        public const string MimeType = "mimetype";

        public const string Tag = "tag";

        /// <summary>"file", "folder" or "drive".</summary>
        public const string Kind = "kind";

        /// <summary>A drive's type: "shared", "organization" or "personal".</summary>
        public const string DriveType = "drivetype";
    }

    private const int MaxLinks = 8;

    public string Name => KindName;

    public bool TryIdentify(object resource, out string id)
    {
        var placement = resource switch
        {
            MediaItem item => item.Placement,
            MediaPlacement found => found,
            _ => null,
        };

        id = placement is null ? string.Empty : placement.LinkTargetPlacementId ?? placement.Id;
        return placement is not null;
    }

    public async Task<ShareableObject?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var placement = await store.GetPlacementAsync(id);
        for (var links = 0; placement?.Placement.LinkTargetPlacementId is { } target; links++)
        {
            if (links == MaxLinks)
            {
                return null;
            }

            placement = await store.GetPlacementAsync(target);
        }

        if (placement is null || await store.GetContentAsync(placement.Placement.ContentId) is not { } content)
        {
            return null;
        }

        var drivePlacement = placement.Placement.DriveId == placement.Placement.Id ? placement : await store.GetPlacementAsync(placement.Placement.DriveId);
        var drive = drivePlacement is null ? null : await store.GetContentAsync(drivePlacement.Placement.ContentId);

        return new ShareableObject
        {
            Ref = new ShareableRef(KindName, placement.Placement.Id),
            Parent = placement.Placement.ParentId is { } parentId ? new ShareableRef(KindName, parentId) : null,
            OwnerUserId = content.Content.Owner.Kind == MediaOwnerKind.User ? content.Content.Owner.Id : null,

            // A placement whose drive is gone is out of use too: fail closed.
            IsInactive = drive is null || drive.Content.IsInactive,
            Facts = FactsOf(placement.Placement, content.Content),
        };
    }

    /// <summary>The placements whose sharing parent is this one. Soft links are not objects of
    /// their own (they are their target), so they are not children.</summary>
    public async Task<IReadOnlyList<ShareableRef>> ChildrenAsync(string id, CancellationToken cancellationToken = default) =>
        [.. (await store.GetChildrenAsync([id]))
            .Where(child => !child.Placement.IsSoftLink)
            .Select(child => new ShareableRef(KindName, child.Placement.Id))];

    /// <summary>Viewing is Viewer, creating and updating Editor, managing (delete, move) Manager.
    /// Changing sharing is Admin, through <see cref="ISharingService"/>.</summary>
    public AccessLevel? LevelFor(string permission) => permission switch
    {
        _ when permission == MediaPermissions.ViewMedia.Name => AccessLevel.Viewer,
        _ when permission == MediaPermissions.EditMedia.Name => AccessLevel.Editor,
        _ when permission == MediaPermissions.ManageMedia.Name ||
               permission == MediaPermissions.ManageOwnMedia.Name ||
               permission == MediaPermissions.ManageOthersMedia.Name ||
               permission == MediaPermissions.ManageMediaFolder.Name => AccessLevel.Manager,
        _ => null,
    };

    private static Dictionary<string, IReadOnlyList<string>> FactsOf(MediaPlacement placement, MediaContent content)
    {
        var facts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Facts.Kind] = [content.Kind.ToString().ToLowerInvariant()],
        };

        if (content.Kind == MediaObjectKind.File)
        {
            var extension = Path.GetExtension(placement.Name).TrimStart('.');
            if (extension.Length > 0)
            {
                facts[Facts.Extension] = [extension.ToLowerInvariant()];
            }

            var version = content.Versions.FirstOrDefault(candidate => candidate.Number == content.PublishedVersion)
                ?? content.Versions.OrderByDescending(candidate => candidate.Number).FirstOrDefault();
            if ((version?.DetectedType ?? version?.ClaimedType) is { Length: > 0 } type)
            {
                facts[Facts.MimeType] = [type.ToLowerInvariant()];
            }
        }

        if (content.DriveType is { } driveType)
        {
            facts[Facts.DriveType] = [driveType.ToString().ToLowerInvariant()];
        }

        if (content.Metadata.Tags is { Count: > 0 } tags)
        {
            facts[Facts.Tag] = [.. tags];
        }

        return facts;
    }
}
