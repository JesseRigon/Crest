namespace Crest.Media.Files;

/// <summary>A placement with its content, as the file-object API returns it.</summary>
public sealed record MediaItem(MediaPlacement Placement, MediaContent Content);

public sealed record MediaUpload(string ParentPlacementId, string Name, Stream Content, string? ClaimedType);

/// <summary>
/// The file-object API (docs/media.md § 3): every read and write as the gated caller, decided by
/// the access gate through sharing on the placement. A placement the caller cannot view does not
/// exist for them (requirement 68). Uploads land in quarantine and become servable on a clean
/// verdict (§ 2). Placements and moves are serialized per tenant and checked against the ancestor
/// closure, so no folder ever contains itself (ruling F4).
/// </summary>
public interface IMediaObjectService
{
    Task<MediaItem> CreateDriveAsync(string name, DriveType type, MediaOwner owner, CancellationToken cancellationToken = default);

    Task<MediaItem> CreateFolderAsync(string parentPlacementId, string name, CancellationToken cancellationToken = default);

    /// <summary>A new file object, its first version quarantined until scanned.</summary>
    Task<MediaItem> UploadAsync(MediaUpload upload, CancellationToken cancellationToken = default);

    /// <summary>A new version of the file a placement holds; earlier versions are kept (requirement 5).</summary>
    Task<MediaItem> ReplaceAsync(string placementId, Stream content, string? claimedType, CancellationToken cancellationToken = default);

    Task<MediaItem?> GetAsync(string placementId, CancellationToken cancellationToken = default);

    /// <summary>The children the caller may view, filtered in the query (requirement 77).</summary>
    Task<IReadOnlyList<MediaItem>> ListChildrenAsync(string placementId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaItem>> ListDrivesAsync(CancellationToken cancellationToken = default);

    Task<MediaItem> RenameAsync(string placementId, string name, CancellationToken cancellationToken = default);

    /// <summary>Manager over everything moved, Editor on the destination (requirement 70).</summary>
    Task<MediaItem> MoveAsync(string placementId, string newParentPlacementId, CancellationToken cancellationToken = default);

    /// <summary>A hard link: another placement of the same content record (files and folders).</summary>
    Task<MediaItem> LinkAsync(string placementId, string newParentPlacementId, string? name = null, CancellationToken cancellationToken = default);

    /// <summary>A soft link: opens the target where it is; no access of its own.</summary>
    Task<MediaItem> ShortcutAsync(string targetPlacementId, string newParentPlacementId, string? name = null, CancellationToken cancellationToken = default);

    /// <summary>Removes one placement; the content goes with its last placement. A folder needs
    /// Manager on everything below it, including what the caller cannot see (requirement 69).</summary>
    Task DeleteAsync(string placementId, CancellationToken cancellationToken = default);

    Task<MediaItem> UpdateMetadataAsync(string placementId, MediaMetadata metadata, CancellationToken cancellationToken = default);

    Task<MediaItem> PublishVersionAsync(string placementId, int version, CancellationToken cancellationToken = default);

    /// <summary>The bytes of a version (the published one by default) after the access check.</summary>
    Task<Stream?> OpenReadAsync(string placementId, int? version = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// The placement graph, baked: an ancestor closure recalculated only on create, move, rename and
/// delete, cached otherwise (ruling F4). Hard-linked folders make it a graph; the loop gate asks
/// it inside the same transaction.
/// </summary>
public interface IMediaGraph
{
    /// <summary>The placement's ancestors, nearest first, up to its drive.</summary>
    Task<IReadOnlyList<string>> AncestorsAsync(string placementId, CancellationToken cancellationToken = default);

    /// <summary>Whether placing <paramref name="contentId"/> under <paramref name="parentPlacementId"/>
    /// would make a folder contain itself, through any of its placements.</summary>
    Task<bool> WouldLoopAsync(string contentId, string parentPlacementId, CancellationToken cancellationToken = default);
}
