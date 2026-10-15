namespace Grove.Media.Files;

/// <summary>What a content record is. Folders and drives are media objects like files; they
/// just have children (docs/media.md, ruling F4).</summary>
public enum MediaObjectKind
{
    File,
    Folder,
    Drive,
}

/// <summary>Which defaults a drive was created with (requirement 13); never changes resolution.</summary>
public enum DriveType
{
    Shared,
    Organization,
    Personal,
}

public enum MediaOwnerKind
{
    /// <summary>A person (a personal drive's owner, what they create in it).</summary>
    User,

    /// <summary>The tenant's system account: owns shared drives.</summary>
    TenantAccount,

    /// <summary>An organization's system account: owns its organization drive.</summary>
    OrganizationAccount,
}

public sealed record MediaOwner(MediaOwnerKind Kind, string? Id = null)
{
    public static readonly MediaOwner Tenant = new(MediaOwnerKind.TenantAccount);

    public static MediaOwner User(string userId) => new(MediaOwnerKind.User, userId);

    public static MediaOwner Organization(string organizationId) => new(MediaOwnerKind.OrganizationAccount, organizationId);
}

/// <summary>Where an upload's bytes are in the security pipeline (docs/media.md § 2).</summary>
public enum MediaVersionState
{
    /// <summary>Held in quarantine, waiting for a verdict: shown to its uploader as pending,
    /// never served or processed.</summary>
    Quarantined,

    /// <summary>Scanned clean and committed: servable.</summary>
    Clean,

    /// <summary>A scanner rejected it; kept only as a record with the reason.</summary>
    Rejected,

    /// <summary>Scanning failed; stays in quarantine until a later verdict.</summary>
    Failed,
}

/// <summary>One immutable version of a file's bytes, stored under an opaque key.</summary>
public sealed record MediaVersion(
    int Number,
    string BlobKey,
    long Size,
    string? ClaimedType,
    string? DetectedType,
    string Sha256,
    MediaVersionState State,
    DateTimeOffset CreatedUtc,
    string? CreatedBy,
    string? Reason = null);

/// <summary>Editable metadata (requirement 4).</summary>
public sealed record MediaMetadata(
    string? Title = null,
    string? Description = null,
    string? AlternativeText = null,
    IReadOnlyList<string>? Tags = null);

/// <summary>
/// The real object: one content record with a stable id, whatever its placements (a hard-linked
/// file has one record and several placements). Files carry versions; folders and drives carry
/// none. A drive also carries its drive type and whether it is inactive.
/// </summary>
public sealed class MediaContent
{
    public required string Id { get; init; }

    public required MediaObjectKind Kind { get; init; }

    public required MediaOwner Owner { get; set; }

    public MediaMetadata Metadata { get; set; } = new();

    public List<MediaVersion> Versions { get; set; } = [];

    /// <summary>The version readers get (the published one); null while none is clean.</summary>
    public int? PublishedVersion { get; set; }

    public DriveType? DriveType { get; set; }

    public bool IsInactive { get; set; }

    public DateTimeOffset CreatedUtc { get; init; }

    public string? CreatedBy { get; init; }

    public DateTimeOffset ModifiedUtc { get; set; }

    public string? ModifiedBy { get; set; }
}

/// <summary>
/// A placement of a content record in a folder (ruling F1): its own id, URL, name, parent and
/// sharing. A drive's placement has no parent. A <see cref="LinkTargetPlacementId"/> makes it
/// a soft link: it opens the target where it is and grants nothing of its own.
/// </summary>
public sealed class MediaPlacement
{
    public required string Id { get; init; }

    public required string ContentId { get; init; }

    public string? ParentId { get; set; }

    public required string DriveId { get; set; }

    public required string Name { get; set; }

    public string? LinkTargetPlacementId { get; init; }

    public bool IsSoftLink => LinkTargetPlacementId is not null;
}
