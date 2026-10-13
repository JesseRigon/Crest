#nullable enable
namespace Crest.Media.Files;

/// <summary>Someone a drive is created for automatically: a staff user (a personal drive) or an
/// organization (an organization drive), with the name the drive gets.</summary>
public sealed record MediaDriveOwner(string Id, string Name);

/// <summary>
/// Who gets a drive automatically (docs/media.md § 3, requirements 12 and 18): the staff users
/// for personal drives, the organizations for organization drives. Implemented by the modules
/// that know them (Members knows user classes and organizations); Media knows neither.
/// </summary>
public interface IMediaDriveOwnerSource
{
    /// <summary>True for a staff user, false for a member (members never have a personal
    /// drive), null when this source does not know the user.</summary>
    Task<bool?> IsStaffAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Every owner of <paramref name="type"/> drives this source knows: staff users for
    /// <see cref="DriveType.Personal"/>, organizations for <see cref="DriveType.Organization"/>.</summary>
    IAsyncEnumerable<MediaDriveOwner> GetOwnersAsync(DriveType type, CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates organization and personal drives with each organization and staff user while the
/// tenant has the drive type on, and for everyone already there when it is turned on
/// (docs/media.md § 3). Runs as the system actor; an owner who already has a drive keeps it.
/// </summary>
public interface IMediaDriveProvisioning
{
    /// <summary>The organization's drive, created when the tenant uses organization drives and
    /// it has none; null when organization drives are off.</summary>
    Task<MediaItem?> EnsureOrganizationDriveAsync(string organizationId, string name, CancellationToken cancellationToken = default);

    /// <summary>The user's personal drive, created when the tenant uses personal drives, the
    /// user is staff and has none; null otherwise.</summary>
    Task<MediaItem?> EnsurePersonalDriveAsync(string userId, string name, CancellationToken cancellationToken = default);

    /// <summary>Creates the missing drives of <paramref name="type"/> for every owner the
    /// sources know; returns how many were created.</summary>
    Task<int> ProvisionAllAsync(DriveType type, CancellationToken cancellationToken = default);
}
