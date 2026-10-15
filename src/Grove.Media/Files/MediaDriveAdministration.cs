#nullable enable
using Grove.Access;
using Grove.Sharing;

namespace Grove.Media.Files;

/// <summary>One page of a listing, and how many the caller may view in all.</summary>
public sealed record MediaPage(IReadOnlyList<MediaItem> Items, int Total);

/// <summary>
/// Writes beyond the contract, for the media library's file store and importer: an overwrite
/// published as soon as it is clean, and the operator's trusted import (recorded clean with a
/// reason, never scanned).
/// </summary>
public interface IMediaObjectWrites : IMediaObjectService
{
    Task<MediaItem> ReplaceAsync(string placementId, Stream content, string? claimedType, bool publishWhenClean, CancellationToken cancellationToken = default);

    Task<MediaItem> ImportTrustedAsync(MediaUpload upload, string reason, CancellationToken cancellationToken = default);
}

/// <summary>Listings paged in the query (requirement 77).</summary>
public interface IMediaObjectListing
{
    Task<MediaPage> ListChildrenPageAsync(string placementId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>One page of the children with the given access indicator (docs/sharing.md,
    /// requirement 98), filtered in the query while sharing is on; with sharing off there are no
    /// indicators and the filter is ignored.</summary>
    Task<MediaPage> ListChildrenPageAsync(string placementId, int skip, int take, Grove.Sharing.ShareIndicator? access, CancellationToken cancellationToken = default) =>
        ListChildrenPageAsync(placementId, skip, take, cancellationToken);
}

/// <summary>What happens to a deleted user's personal drive (requirement 20).</summary>
public enum PersonalDriveDisposal
{
    /// <summary>To another user: their personal drive, or a folder in it when they have one.</summary>
    TransferToUser,

    /// <summary>Into a folder of a shared drive, owned by the tenant from then on.</summary>
    MoveToSharedDrive,

    /// <summary>Kept, out of use, with its contents and sharing.</summary>
    KeepInactive,

    Delete,
}

/// <summary>
/// The tenant administrator's drive operations (docs/media.md § 3 and § 7): inactive drives
/// (requirements 21-23), reactivation with the choice of keeping or resetting sharing (22), and
/// what happens to a deleted user's personal drive (20). None of them opens a drive's contents.
/// </summary>
public interface IMediaDriveAdministration
{
    /// <summary>A user's personal drive (the drive itself, not what is in it), for the separate
    /// "open a user's drive" step.</summary>
    Task<MediaItem?> FindPersonalDriveAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Makes a drive inactive, or active again keeping its sharing or resetting it to
    /// its drive type's defaults (requirement 22).</summary>
    Task SetDriveInactiveAsync(string drivePlacementId, bool inactive, bool resetSharing = false, CancellationToken cancellationToken = default);

    /// <summary>A drive type turned off makes its drives inactive (requirement 21); turned on,
    /// active again, keeping or resetting their sharing (22). Returns how many changed.</summary>
    Task<int> SetDriveTypeEnabledAsync(DriveType type, bool enabled, bool resetSharing = false, CancellationToken cancellationToken = default);

    /// <summary>The choices offered when a user is deleted: all four until the retention system
    /// decides which a tenant allows (requirement 20).</summary>
    IReadOnlyList<PersonalDriveDisposal> DisposalChoices { get; }

    /// <param name="targetId">The user (<see cref="PersonalDriveDisposal.TransferToUser"/>) or
    /// the shared drive placement (<see cref="PersonalDriveDisposal.MoveToSharedDrive"/>).</param>
    Task DisposePersonalDriveAsync(string userId, PersonalDriveDisposal disposal, string? targetId = null, CancellationToken cancellationToken = default);
}

public sealed partial class MediaObjectService : IMediaDriveAdministration
{
    public IReadOnlyList<PersonalDriveDisposal> DisposalChoices { get; } =
        [PersonalDriveDisposal.TransferToUser, PersonalDriveDisposal.MoveToSharedDrive, PersonalDriveDisposal.KeepInactive, PersonalDriveDisposal.Delete];

    public async Task<MediaItem?> FindPersonalDriveAsync(string userId, CancellationToken cancellationToken = default)
    {
        TenantAdministrator();
        return await PersonalDriveOfAsync(userId) is { } drive ? await ItemAsync(drive) : null;
    }

    public Task SetDriveInactiveAsync(string drivePlacementId, bool inactive, bool resetSharing = false, CancellationToken cancellationToken = default)
    {
        TenantAdministrator();
        return SerializedAsync(GraphLock, async () =>
        {
            var drive = await DriveRootAsync(drivePlacementId);
            await SetInactiveAsync(drive, inactive, resetSharing, cancellationToken);
            return true;
        });
    }

    public Task<int> SetDriveTypeEnabledAsync(DriveType type, bool enabled, bool resetSharing = false, CancellationToken cancellationToken = default)
    {
        TenantAdministrator();
        return SerializedAsync(GraphLock, async () =>
        {
            var changed = 0;
            foreach (var content in await store.GetDriveContentsAsync(type))
            {
                if (content.Content.IsInactive == !enabled)
                {
                    continue;
                }

                foreach (var drive in await store.GetPlacementsOfContentAsync(content.Content.Id))
                {
                    await SetInactiveAsync(drive, !enabled, resetSharing, cancellationToken);
                }

                changed++;
            }

            return changed;
        });
    }

    public Task DisposePersonalDriveAsync(string userId, PersonalDriveDisposal disposal, string? targetId = null, CancellationToken cancellationToken = default)
    {
        var caller = TenantAdministrator();
        return SerializedAsync(GraphLock, async () =>
        {
            var drive = await PersonalDriveOfAsync(userId) ?? throw new MediaObjectNotFoundException();
            var content = await store.GetContentAsync(drive.Placement.ContentId) ?? throw new MediaObjectNotFoundException();
            var previous = MediaOwner.User(userId);

            switch (disposal)
            {
                case PersonalDriveDisposal.KeepInactive:
                    await SetInactiveAsync(drive, inactive: true, resetSharing: false, cancellationToken);
                    break;

                case PersonalDriveDisposal.Delete:
                    await RemoveTreeAsync(drive, cancellationToken);
                    break;

                case PersonalDriveDisposal.TransferToUser:
                    if (string.IsNullOrEmpty(targetId) || targetId == userId)
                    {
                        throw new ArgumentException("Name the user who receives the drive.", nameof(targetId));
                    }

                    var next = MediaOwner.User(targetId);
                    if (await PersonalDriveOfAsync(targetId) is { } theirs)
                    {
                        // They have a drive: everything moves into a folder of it.
                        await MoveDriveIntoAsync(drive, theirs, next, previous, caller, cancellationToken);
                    }
                    else
                    {
                        await ReownAsync(drive, previous, next);
                        content.Content.Owner = next;
                        await store.SaveAsync(content);
                        await IndexAsync(drive, below: true, cancellationToken);
                    }

                    break;

                case PersonalDriveDisposal.MoveToSharedDrive:
                    var shared = targetId is null ? null : await store.GetPlacementAsync(targetId);
                    var sharedContent = shared is null ? null : await store.GetContentAsync(shared.Placement.ContentId);
                    if (shared?.Placement.ParentId is not null || sharedContent?.Content.DriveType != DriveType.Shared)
                    {
                        throw new ArgumentException("Name the shared drive that receives the files.", nameof(targetId));
                    }

                    await MoveDriveIntoAsync(drive, shared!, MediaOwner.Tenant, previous, caller, cancellationToken);
                    break;
            }

            return true;
        });
    }

    // ---- Shared helpers -------------------------------------------------------------------

    private IShareAccessIndex? AccessIndex => accessIndexes.LastOrDefault();

    /// <summary>Sharing's list filter for the caller; null while sharing does not govern file
    /// lists (the feature is off), and the media permissions decide object by object.</summary>
    private ScopeRule? Scope(CallerContext caller) =>
        shareScopes.LastOrDefault()?.RuleFor(caller, FileShareableKind.Name, AccessLevel.Viewer);

    private static ShareableRef Ref(MediaPlacementDocument placement) => new(FileShareableKind.Name, placement.Placement.Id);

    /// <summary>Recalculates the placement's access (and everything below it) after a change.
    /// A soft link has none of its own.</summary>
    private async Task IndexAsync(MediaPlacementDocument placement, bool below, CancellationToken cancellationToken)
    {
        if (AccessIndex is { } index && !placement.Placement.IsSoftLink)
        {
            await index.RecalculateAsync(Ref(placement), below, cancellationToken);
        }
    }

    /// <summary>A content fact changed: every placement of it reads it.</summary>
    private async Task IndexContentAsync(string contentId, CancellationToken cancellationToken)
    {
        if (AccessIndex is null)
        {
            return;
        }

        foreach (var placement in await store.GetPlacementsOfContentAsync(contentId))
        {
            await IndexAsync(placement, below: false, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<MediaItem>> ItemsAsync(IEnumerable<MediaPlacementDocument> placements)
    {
        var list = placements.ToList();
        var contents = (await store.GetContentsAsync([.. list.Select(placement => placement.Placement.ContentId)]))
            .ToDictionary(content => content.Content.Id, StringComparer.Ordinal);
        return [.. list
            .Where(placement => contents.ContainsKey(placement.Placement.ContentId))
            .Select(placement => new MediaItem(placement.Placement, contents[placement.Placement.ContentId].Content))];
    }

    /// <summary>Removes a placement with what goes with it, keeping the closure and the access
    /// index right for what survives (re-parented children, content placed elsewhere too).</summary>
    private async Task RemoveTreeAsync(MediaPlacementDocument placement, CancellationToken cancellationToken)
    {
        if (AccessIndex is { } index && !placement.Placement.IsSoftLink)
        {
            await index.RemoveAsync(Ref(placement), includeDescendants: true, cancellationToken);
        }

        var recalculate = new Dictionary<string, MediaPlacementDocument>(StringComparer.Ordinal);
        var removed = new HashSet<string>(StringComparer.Ordinal);
        await RemoveAsync(placement, recalculate, removed);
        var survivors = recalculate.Values.Where(document => !removed.Contains(document.Placement.Id)).ToList();
        await graph.RecalculateAsync(survivors);
        foreach (var survivor in survivors)
        {
            await IndexAsync(survivor, below: true, cancellationToken);
        }
    }

    private CallerContext TenantAdministrator() =>
        callers.Current is { IsSuperUser: true } caller
            ? caller
            : throw new AccessDeniedException(MediaPermissions.ManageSharedDrives.Name, "Only a tenant administrator changes drives this way.");

    private async Task<MediaPlacementDocument> DriveRootAsync(string drivePlacementId)
    {
        var drive = await store.GetPlacementAsync(drivePlacementId);
        return drive is { Placement: { ParentId: null, IsSoftLink: false } } ? drive : throw new MediaObjectNotFoundException();
    }

    private async Task<MediaPlacementDocument?> PersonalDriveOfAsync(string userId) =>
        await store.GetPersonalDriveAsync(userId) is { } content
            ? (await store.GetPlacementsOfContentAsync(content.Content.Id)).FirstOrDefault()
            : null;

    private async Task SetInactiveAsync(MediaPlacementDocument drive, bool inactive, bool resetSharing, CancellationToken cancellationToken)
    {
        var content = await store.GetContentAsync(drive.Placement.ContentId) ?? throw new MediaObjectNotFoundException();
        content.Content.IsInactive = inactive;
        await store.SaveAsync(content);
        _drives.Remove(drive.Placement.Id);

        // Reactivating asks whether to keep the drive's sharing or start from its drive type's
        // defaults (requirement 22): the defaults are the owner's own rights and, for an
        // organization drive, its admins' (contributed, never stored), so resetting removes
        // every stored entry and rule on the drive and below it.
        if (!inactive && resetSharing && sharing.LastOrDefault() is { } service)
        {
            foreach (var placement in (await store.GetDescendantsAsync(drive.Placement.Id)).Prepend(drive).Where(placement => !placement.Placement.IsSoftLink))
            {
                var target = Ref(placement);
                foreach (var entry in await service.GetEntriesAsync(target, cancellationToken))
                {
                    await service.RemoveEntryAsync(target, entry.Principal, cancellationToken);
                }

                await service.SetRulesAsync(target, [], cancellationToken);
            }
        }

        // Inactive is a fact of everything in the drive.
        await IndexAsync(drive, below: true, cancellationToken);
    }

    /// <summary>Moves everything in <paramref name="drive"/> into a new folder of
    /// <paramref name="into"/>, hands what the previous owner owned to <paramref name="owner"/>,
    /// and removes the emptied drive.</summary>
    private async Task MoveDriveIntoAsync(MediaPlacementDocument drive, MediaPlacementDocument into, MediaOwner owner, MediaOwner previous, CallerContext caller, CancellationToken cancellationToken)
    {
        await ReownAsync(drive, previous, owner);

        var folder = NewContent(MediaObjectKind.Folder, owner, caller);
        var placement = NewPlacement(folder.Content.Id, into, drive.Placement.Name, MediaObjectKind.Folder);
        await store.SaveAsync(folder);
        await store.SaveAsync(placement);

        foreach (var child in await store.GetChildrenAsync([drive.Placement.Id]))
        {
            child.Placement.ParentId = placement.Placement.Id;
            await store.SaveAsync(child);
        }

        await graph.RecalculateAsync([placement]);
        await IndexAsync(placement, below: true, cancellationToken);

        if (AccessIndex is { } index)
        {
            await index.RemoveAsync(Ref(drive), includeDescendants: false, cancellationToken);
        }

        await store.DeleteAsync(drive);
        if (await store.GetContentAsync(drive.Placement.ContentId) is { } emptied)
        {
            await store.DeleteAsync(emptied);
        }
    }

    private async Task ReownAsync(MediaPlacementDocument drive, MediaOwner previous, MediaOwner next)
    {
        var below = await store.GetDescendantsAsync(drive.Placement.Id);
        foreach (var content in await store.GetContentsAsync([.. below.Select(placement => placement.Placement.ContentId)]))
        {
            if (content.Content.Owner == previous)
            {
                content.Content.Owner = next;
                await store.SaveAsync(content);
            }
        }
    }
}
