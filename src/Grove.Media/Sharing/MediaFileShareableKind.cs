#nullable enable
using Grove.Media.Files;
using Grove.Sharing;

namespace Grove.Media.Sharing;

/// <summary>
/// File objects as a shareable kind (docs/media.md § 3, docs/sharing.md): an object is a
/// placement, so each placement of a hard-linked file has its own sharing, inherited down its
/// own path (ruling F1). The parent is the placement's folder; the root is its drive, where
/// Drive Admin is granted. The owner is the user for what is in a personal drive (everything
/// there is theirs, requirement 14) and nobody for what a system account owns. Everything in
/// an inactive drive is inactive. A soft link is identified as its target, so it opens the
/// target with the target's access and grants nothing of its own.
/// </summary>
public sealed class MediaFileShareableKind(IMediaObjectStore store, IServiceProvider? services = null) : IShareableKind, IShareableTree, IShareableScopeTarget, IShareableRoots, IShareableAdminAssignment, IShareableAdminEligibility, IShareableLinks
{
    /// <summary>The byte endpoint (docs/media.md › Serving): the Share panel's public link.</summary>
    public Task<string?> LinkAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>($"files/{Uri.EscapeDataString(id)}");

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
            DisplayName = placement.Placement.Name,
            Parent = placement.Placement.ParentId is { } parentId ? new ShareableRef(KindName, parentId) : null,
            OtherParents = await OtherParentsAsync(placement.Placement),
            OwnerUserId = content.Content.Owner.Kind == MediaOwnerKind.User ? content.Content.Owner.Id : null,
            OwnerAccountId = content.Content.Owner.Kind switch
            {
                MediaOwnerKind.TenantAccount => Grove.Access.SystemAccounts.Tenant,
                MediaOwnerKind.OrganizationAccount when content.Content.Owner.Id is { } organizationId => Grove.Access.SystemAccounts.Organization(organizationId),
                _ => null,
            },

            // A placement whose drive is gone is out of use too: fail closed.
            IsInactive = drive is null || drive.Content.IsInactive,
            Facts = FactsOf(placement.Placement, content.Content),
        };
    }

    /// <summary>
    /// What inherits from this placement: the children of its folder through every hard
    /// placement of the folder (multi-path inheritance, F3: they inherit through each of them).
    /// Soft links are not objects of their own (they are their target), so they are not children.
    /// </summary>
    public async Task<IReadOnlyList<ShareableRef>> ChildrenAsync(string id, CancellationToken cancellationToken = default) =>
        [.. (await store.GetChildrenAsync(await HardPlacementsOfSameContentAsync(id)))
            .Where(child => !child.Placement.IsSoftLink)
            .Select(child => new ShareableRef(KindName, child.Placement.Id))];

    /// <summary>
    /// The folder's other hard placements, beside the one the placement was created or moved
    /// through (its sharing parent): a child of a hard-linked folder inherits through each of
    /// them too (multi-path inheritance, F3).
    /// </summary>
    private async Task<IReadOnlyList<ShareableRef>> OtherParentsAsync(MediaPlacement placement)
    {
        if (placement.ParentId is not { } parentId)
        {
            return [];
        }

        var placements = await HardPlacementsOfSameContentAsync(parentId);
        var parents = placements.Where(other => other != parentId).Select(other => new ShareableRef(KindName, other)).ToList();
        if (await AttachedContentItemAsync(placement) is { } contentItemId)
        {
            parents.Add(new ShareableRef(ContentKindName, contentItemId));
        }

        return parents;
    }

    /// <summary>The content kind's name (<c>Grove.Contents</c>' sharing), when it is registered.</summary>
    public const string ContentKindName = "content";

    /// <summary>
    /// The content item an attached-field folder belongs to (<c>mediafields/{type}/{id}</c> at
    /// the root of the media library drive): its files follow their content item's access
    /// (docs/media.md › The media library (F1)), so the item is one more parent of the folder.
    /// A path through it is left out when content items are not shareable.
    /// </summary>
    private async Task<string?> AttachedContentItemAsync(MediaPlacement placement)
    {
        if (placement.ParentId is not { } typeFolderId ||
            await store.GetPlacementAsync(typeFolderId) is not { } typeFolder ||
            typeFolder.Placement.ParentId is not { } attachedRootId ||
            await store.GetPlacementAsync(attachedRootId) is not { } attachedRoot ||
            attachedRoot.Placement.ParentId != placement.DriveId ||
            !string.Equals(attachedRoot.Placement.Name, AttachedFieldsFolder, StringComparison.Ordinal))
        {
            return null;
        }

        var library = services?.GetService(typeof(Library.IMediaLibrary)) as Library.IMediaLibrary;
        return library is not null && await library.GetDriveIdAsync() == placement.DriveId ? placement.Name : null;
    }

    /// <summary>Where attached media fields keep their files (<c>AttachedMediaFieldFileService</c>).</summary>
    private const string AttachedFieldsFolder = "mediafields";

    /// <summary>The placement and every other hard placement of its content, in a stable order.</summary>
    private async Task<IReadOnlyList<string>> HardPlacementsOfSameContentAsync(string placementId)
    {
        if (await store.GetPlacementAsync(placementId) is not { } placement)
        {
            return [placementId];
        }

        var all = await store.GetPlacementsOfContentAsync(placement.Placement.ContentId);
        return [placementId, .. all
            .Where(other => !other.Placement.IsSoftLink && other.Placement.Id != placementId)
            .Select(other => other.Placement.Id)
            .Order(StringComparer.Ordinal)];
    }

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
