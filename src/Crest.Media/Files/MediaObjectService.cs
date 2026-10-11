#nullable enable
using System.Security.Cryptography;
using Crest.Access;
using Crest.Locking.Distributed;
using Crest.Modules;
using Crest.Sharing;
using Microsoft.Extensions.Logging;

namespace Crest.Media.Files;

/// <summary>
/// The file-object API (docs/media.md § 3) as the gated caller. Every check asks the access
/// decision with the placement as the resource, so sharing decides while it is on (the "file"
/// kind) and the media permissions decide while it is off. A placement the caller cannot view
/// does not exist for them: reads return nothing and writes throw
/// <see cref="MediaObjectNotFoundException"/>. Viewing takes <c>ViewMedia</c> (Viewer); creating
/// and updating take <c>EditMedia</c> (Editor); deleting and moving take <c>ManageMedia</c>
/// (Manager). A soft link is read through its target; changing the link itself (rename, move,
/// delete) is a change to the folder it sits in. Placement changes are serialized per tenant
/// and committed before the lock is released, so the loop gate never races (ruling F4).
/// While sharing is on, lists filter in the query through sharing's scope and every change that
/// moves an object or a fact a rule reads recalculates the access index (requirements 77-78).
/// A tenant administrator reaches another user's personal drive only through an administrator
/// access in force (§ 7); without one it does not exist for them.
/// </summary>
public sealed partial class MediaObjectService(
    IMediaObjectStore store,
    MediaGraph graph,
    IMediaStorageProvider storage,
    IAccessDecision decision,
    ICallerContextAccessor callers,
    IDistributedLock locks,
    IClock clock,
    IFileDriveSettingsAccessor driveSettings,
    IEnumerable<IUploadQuarantine> quarantines,
    ILogger<MediaObjectService> logger,
    IPersonalDriveAccessService personalAccess,
    IEnumerable<IAccessAuditor> auditors,
    IEnumerable<IShareAccessIndex> accessIndexes,
    IEnumerable<IShareScopes> shareScopes,
    IEnumerable<ISharingService> sharing) : IMediaObjectWrites, IMediaObjectListing, IMediaVerdictHandler
{
    private const string GraphLock = "media-files-graph";
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LockExpiration = TimeSpan.FromMinutes(2);

    private static readonly string View = MediaPermissions.ViewMedia.Name;
    private static readonly string Edit = MediaPermissions.EditMedia.Name;
    private static readonly string Manage = MediaPermissions.ManageMedia.Name;

    private readonly Dictionary<string, MediaContentDocument?> _drives = new(StringComparer.Ordinal);

    // ---- Drives ---------------------------------------------------------------------------

    public Task<MediaItem> CreateDriveAsync(string name, DriveType type, MediaOwner owner, CancellationToken cancellationToken = default)
    {
        var caller = Caller();
        name = MediaNames.Validate(name);
        return SerializedAsync(GraphLock, async () =>
        {
            if (!(await driveSettings.GetAsync()).Allows(type))
            {
                throw new MediaObjectConflictException($"{type} drives are turned off for this tenant.");
            }

            switch (type)
            {
                case DriveType.Shared when owner.Kind != MediaOwnerKind.TenantAccount:
                    throw new ArgumentException("A shared drive is owned by the tenant account.", nameof(owner));
                case DriveType.Organization when owner.Kind != MediaOwnerKind.OrganizationAccount || string.IsNullOrEmpty(owner.Id):
                    throw new ArgumentException("An organization drive is owned by its organization's account.", nameof(owner));
                case DriveType.Personal when owner.Kind != MediaOwnerKind.User || string.IsNullOrEmpty(owner.Id):
                    throw new ArgumentException("A personal drive is owned by its user.", nameof(owner));
            }

            if (type == DriveType.Personal && owner.Id == caller.UserId)
            {
                // Members never have a personal drive (requirement 18).
                if (caller.UserClass == CallerClasses.Member)
                {
                    throw new AccessDeniedException(MediaPermissions.ManageSharedDrives.Name, "Members have no personal drive.");
                }
            }
            else
            {
                // Creating a drive for the tenant, an organization or someone else (requirement 31).
                await decision.RequireAsync(caller, MediaPermissions.ManageSharedDrives.Name, cancellationToken: cancellationToken);
            }

            if (type == DriveType.Personal && await store.GetPersonalDriveAsync(owner.Id!) is not null)
            {
                throw new MediaObjectConflictException("The user already has a personal drive.");
            }

            var content = NewContent(MediaObjectKind.Drive, owner, caller);
            content.Content.DriveType = type;
            var placementId = IdGenerator.GenerateId();
            var placement = new MediaPlacementDocument
            {
                Placement = new MediaPlacement { Id = placementId, ContentId = content.Content.Id, DriveId = placementId, Name = name },
                Kind = MediaObjectKind.Drive,
            };

            await store.SaveAsync(content);
            await store.SaveAsync(placement);
            await graph.RecalculateAsync([placement]);
            await IndexAsync(placement, below: false, cancellationToken);

            // An organization's admins are its drive's Drive Admins by default and follow the
            // organization (requirement 17): the admin role scoped to that organization matches
            // only those holding it while acting for it, so Admin comes and goes with the binding.
            if (type == DriveType.Organization && sharing.LastOrDefault() is { } service)
            {
                var role = (await driveSettings.GetAsync()).OrganizationAdminRole;
                await service.SetEntryAsync(Ref(placement), ShareEntry.Grant(SharePrincipal.Role(role, owner.Id), AccessLevel.Admin), cancellationToken);
            }

            return new MediaItem(placement.Placement, content.Content);
        });
    }

    public async Task<IReadOnlyList<MediaItem>> ListDrivesAsync(CancellationToken cancellationToken = default)
    {
        var caller = Caller();
        var scope = Scope(caller);
        var drives = scope is null ? await store.GetDrivesAsync() : await store.GetDrivesAsync(scope);
        var contents = (await store.GetContentsAsync([.. drives.Select(drive => drive.Placement.ContentId)]))
            .ToDictionary(content => content.Content.Id, StringComparer.Ordinal);

        var visible = new List<MediaItem>();
        foreach (var drive in drives.OrderBy(drive => drive.Placement.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!contents.TryGetValue(drive.Placement.ContentId, out var content))
            {
                continue;
            }

            _drives[drive.Placement.Id] = content;

            // Other users' personal drives never appear in the everyday view, not even a tenant
            // administrator's (requirement 24); they are opened through the separate access flow.
            if (content.Content.DriveType == DriveType.Personal && content.Content.Owner.Id != caller.UserId)
            {
                continue;
            }

            // Filtered in the query while sharing is on; otherwise the media permissions decide
            // each drive (there are few).
            if (scope is null ? await CanAsync(caller, View, drive, cancellationToken) : caller.IsSuperUser || !content.Content.IsInactive)
            {
                visible.Add(new MediaItem(drive.Placement, content.Content));
            }
        }

        return visible;
    }

    // ---- Reads ----------------------------------------------------------------------------

    public async Task<MediaItem?> GetAsync(string placementId, CancellationToken cancellationToken = default)
    {
        var caller = Caller();
        var placement = await store.GetPlacementAsync(placementId);
        if (placement is null || !await CanAsync(caller, View, placement, cancellationToken))
        {
            return null;
        }

        return await ItemAsync(placement);
    }

    public async Task<IReadOnlyList<MediaItem>> ListChildrenAsync(string placementId, CancellationToken cancellationToken = default) =>
        (await ListChildrenPageAsync(placementId, 0, int.MaxValue, cancellationToken)).Items;

    /// <summary>
    /// One page of the children the caller may view (requirement 77). While sharing is on the
    /// filter is sharing's scope over the placement index, applied and paged in the query. With
    /// sharing off the media permissions decide, object by object, and the page is cut from the
    /// filtered list, never from the loaded one.
    /// </summary>
    public async Task<MediaPage> ListChildrenPageAsync(string placementId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var caller = Caller();
        skip = Math.Max(0, skip);
        take = Math.Max(0, take);
        var folder = await TargetAsync(await VisibleAsync(caller, placementId, cancellationToken), caller, cancellationToken);
        var holders = (await store.GetPlacementsOfContentAsync(folder.Placement.ContentId)).Select(holder => holder.Placement.Id).ToList();

        if (Scope(caller) is { } scope)
        {
            var (page, total) = await store.GetChildrenAsync(holders, scope, skip, take);
            return new MediaPage(await ItemsAsync(page), total);
        }

        var visible = new List<MediaPlacementDocument>();
        foreach (var child in await store.GetChildrenAsync(holders))
        {
            if (await CanAsync(caller, View, child, cancellationToken))
            {
                visible.Add(child);
            }
        }

        var ordered = visible
            .OrderByDescending(child => child.Kind)
            .ThenBy(child => child.Placement.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(child => child.Placement.Id, StringComparer.Ordinal)
            .ToList();
        return new MediaPage(await ItemsAsync(ordered.Skip(skip).Take(take)), ordered.Count);
    }

    public async Task<Stream?> OpenReadAsync(string placementId, int? version = null, CancellationToken cancellationToken = default)
    {
        var caller = Caller();
        var placement = await store.GetPlacementAsync(placementId);
        if (placement is null || !await CanAsync(caller, View, placement, cancellationToken))
        {
            return null;
        }

        var content = await store.GetContentAsync(placement.Placement.ContentId);
        var number = version ?? content?.Content.PublishedVersion;
        if (content is null || number is null || content.Content.Versions.FirstOrDefault(candidate => candidate.Number == number) is not { State: MediaVersionState.Clean } found)
        {
            return null;
        }

        // Readers get the published version; drafts are for Editors and Managers.
        if (number != content.Content.PublishedVersion && !await CanAsync(caller, Edit, placement, cancellationToken))
        {
            return null;
        }

        return await storage.OpenReadAsync(MediaStorageArea.Committed, found.BlobKey, cancellationToken);
    }

    // ---- Creating -------------------------------------------------------------------------

    public Task<MediaItem> CreateFolderAsync(string parentPlacementId, string name, CancellationToken cancellationToken = default)
    {
        var caller = Caller();
        name = MediaNames.Validate(name);
        return SerializedAsync(GraphLock, async () =>
        {
            var parent = await ContainerAsync(caller, parentPlacementId, cancellationToken);
            await RequireAsync(caller, Edit, parent, cancellationToken);

            var content = NewContent(MediaObjectKind.Folder, await OwnerUnderAsync(parent), caller);
            var placement = NewPlacement(content.Content.Id, parent, name, MediaObjectKind.Folder);
            await store.SaveAsync(content);
            await store.SaveAsync(placement);
            await graph.RecalculateAsync([placement]);
            await IndexAsync(placement, below: false, cancellationToken);
            return new MediaItem(placement.Placement, content.Content);
        });
    }

    public Task<MediaItem> UploadAsync(MediaUpload upload, CancellationToken cancellationToken = default) =>
        UploadCoreAsync(upload, trustedReason: null, cancellationToken);

    /// <summary>
    /// A new file object whose first version is recorded clean with <paramref name="reason"/>
    /// and never handed to the scanners: either scanned or trusted, never both. For the system
    /// actor only (the operator's <c>TrustExistingFiles</c> import of bytes already served).
    /// </summary>
    public Task<MediaItem> ImportTrustedAsync(MediaUpload upload, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Caller() is not { IsSystem: true })
        {
            throw new AccessDeniedException(Edit, "Only the system imports files without a scan.");
        }

        return UploadCoreAsync(upload, reason, cancellationToken);
    }

    private async Task<MediaItem> UploadCoreAsync(MediaUpload upload, string? trustedReason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        var caller = Caller();
        var name = MediaNames.Validate(upload.Name);

        // Checked before the bytes are taken, and again under the lock before placing them.
        var parent = await ContainerAsync(caller, upload.ParentPlacementId, cancellationToken);
        await RequireAsync(caller, Edit, parent, cancellationToken);

        var contentId = IdGenerator.GenerateId();
        var version = await QuarantineAsync(contentId, 1, name, upload.Content, upload.ClaimedType, caller, submit: trustedReason is null, cancellationToken);
        MediaItem item;
        try
        {
            item = await SerializedAsync(GraphLock, async () =>
            {
                parent = await ContainerAsync(caller, upload.ParentPlacementId, cancellationToken);
                await RequireAsync(caller, Edit, parent, cancellationToken);

                var content = NewContent(MediaObjectKind.File, await OwnerUnderAsync(parent), caller, contentId);
                content.Content.Versions.Add(version);
                var placement = NewPlacement(contentId, parent, name, MediaObjectKind.File);
                await store.SaveAsync(content);
                await store.SaveAsync(placement);
                await graph.RecalculateAsync([placement]);
                await IndexAsync(placement, below: false, cancellationToken);
                return new MediaItem(placement.Placement, content.Content);
            });
        }
        catch
        {
            // A verdict for it will find no content and be dropped.
            await storage.DeleteAsync(MediaStorageArea.Quarantine, version.BlobKey, CancellationToken.None);
            throw;
        }

        if (trustedReason is null)
        {
            await CommitUnscannedAsync(contentId, version, cancellationToken);
        }
        else
        {
            await VerdictCoreAsync(contentId, version.Number, ScanVerdict.Clean, trustedReason, cancellationToken);
        }

        return await ItemAsync(await store.GetPlacementAsync(item.Placement.Id) ?? throw new MediaObjectNotFoundException());
    }

    public Task<MediaItem> ReplaceAsync(string placementId, Stream content, string? claimedType, CancellationToken cancellationToken = default) =>
        ReplaceAsync(placementId, content, claimedType, publishWhenClean: false, cancellationToken);

    /// <summary>A new version; with <paramref name="publishWhenClean"/> it becomes the published
    /// one as soon as its verdict is clean (an overwrite, which replaces what is served).</summary>
    public async Task<MediaItem> ReplaceAsync(string placementId, Stream content, string? claimedType, bool publishWhenClean, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var caller = Caller();
        var placement = await VisibleAsync(caller, placementId, cancellationToken);
        await RequireAsync(caller, Edit, placement, cancellationToken);
        var contentId = placement.Placement.ContentId;

        MediaVersion version = null!;
        await SerializedAsync(ContentLock(contentId), async () =>
        {
            var document = await store.GetContentAsync(contentId) ?? throw new MediaObjectNotFoundException();
            if (document.Content.Kind != MediaObjectKind.File)
            {
                throw new MediaObjectConflictException("Only a file has versions.");
            }

            var number = document.Content.Versions.Count == 0 ? 1 : document.Content.Versions.Max(candidate => candidate.Number) + 1;
            version = await QuarantineAsync(contentId, number, placement.Placement.Name, content, claimedType, caller, submit: true, cancellationToken);
            document.Content.Versions.Add(version);
            if (publishWhenClean)
            {
                document.PublishWhenClean.Add(number);
            }

            Touch(document, caller);
            await store.SaveAsync(document);
            await IndexContentAsync(contentId, cancellationToken);
            return true;
        });

        await CommitUnscannedAsync(contentId, version, cancellationToken);
        return await ItemAsync(placement);
    }

    // ---- Changing placements --------------------------------------------------------------

    public Task<MediaItem> RenameAsync(string placementId, string name, CancellationToken cancellationToken = default)
    {
        var caller = Caller();
        name = MediaNames.Validate(name);
        return SerializedAsync(GraphLock, async () =>
        {
            var placement = await VisibleAsync(caller, placementId, cancellationToken);
            await RequireAsync(caller, Edit, await SelfAsync(placement), cancellationToken);

            // Names are not in the closure, so a rename recalculates nothing there (ruling F4);
            // the extension is a fact rules read, so the access index is.
            placement.Placement.Name = name;
            await store.SaveAsync(placement);
            await IndexAsync(placement, below: true, cancellationToken);
            return await ItemAsync(placement);
        });
    }

    public Task<MediaItem> MoveAsync(string placementId, string newParentPlacementId, CancellationToken cancellationToken = default)
    {
        var caller = Caller();
        return SerializedAsync(GraphLock, async () =>
        {
            var placement = await VisibleAsync(caller, placementId, cancellationToken);
            if (placement.Placement.ParentId is null)
            {
                throw new MediaObjectConflictException("A drive cannot be moved.");
            }

            var destination = await ContainerAsync(caller, newParentPlacementId, cancellationToken);

            // Moving changes the access of everything moved: Manager over all of it, Editor on
            // the destination (requirement 70).
            await RequireManagerBelowAsync(caller, placement, cancellationToken);
            await RequireAsync(caller, Edit, destination, cancellationToken);
            if (!placement.Placement.IsSoftLink && await graph.WouldLoopAsync(placement.Placement.ContentId, destination.Placement.Id, cancellationToken))
            {
                throw new MediaObjectConflictException("A folder cannot be placed inside itself.");
            }

            placement.Placement.ParentId = destination.Placement.Id;
            await graph.RecalculateAsync([placement]);
            await IndexAsync(placement, below: true, cancellationToken);
            return await ItemAsync(placement);
        });
    }

    public Task<MediaItem> LinkAsync(string placementId, string newParentPlacementId, string? name = null, CancellationToken cancellationToken = default)
    {
        var caller = Caller();
        return SerializedAsync(GraphLock, async () =>
        {
            var source = await TargetAsync(await VisibleAsync(caller, placementId, cancellationToken), caller, cancellationToken);
            if (source.Placement.ParentId is null)
            {
                throw new MediaObjectConflictException("A drive cannot be placed in a folder.");
            }

            var destination = await ContainerAsync(caller, newParentPlacementId, cancellationToken);

            // Another placement reaches the content from another path, as a move does.
            await RequireAsync(caller, Manage, source, cancellationToken);
            await RequireAsync(caller, Edit, destination, cancellationToken);
            if (await graph.WouldLoopAsync(source.Placement.ContentId, destination.Placement.Id, cancellationToken))
            {
                throw new MediaObjectConflictException("A folder cannot be placed inside itself.");
            }

            var placement = NewPlacement(source.Placement.ContentId, destination, name is null ? source.Placement.Name : MediaNames.Validate(name), source.Kind);
            await store.SaveAsync(placement);
            await graph.RecalculateAsync([placement]);
            await IndexAsync(placement, below: true, cancellationToken);
            return await ItemAsync(placement);
        });
    }

    public Task<MediaItem> ShortcutAsync(string targetPlacementId, string newParentPlacementId, string? name = null, CancellationToken cancellationToken = default)
    {
        var caller = Caller();
        return SerializedAsync(GraphLock, async () =>
        {
            var target = await TargetAsync(await VisibleAsync(caller, targetPlacementId, cancellationToken), caller, cancellationToken);
            var destination = await ContainerAsync(caller, newParentPlacementId, cancellationToken);
            await RequireAsync(caller, Edit, destination, cancellationToken);

            // A soft link is a leaf: it opens the target where it is and grants nothing, so it
            // never makes a folder contain itself.
            var placement = new MediaPlacementDocument
            {
                Placement = new MediaPlacement
                {
                    Id = IdGenerator.GenerateId(),
                    ContentId = target.Placement.ContentId,
                    ParentId = destination.Placement.Id,
                    DriveId = destination.Placement.DriveId,
                    Name = name is null ? target.Placement.Name : MediaNames.Validate(name),
                    LinkTargetPlacementId = target.Placement.Id,
                },
                Kind = target.Kind,
            };
            await store.SaveAsync(placement);
            await graph.RecalculateAsync([placement]);
            return await ItemAsync(placement);
        });
    }

    public Task DeleteAsync(string placementId, CancellationToken cancellationToken = default)
    {
        var caller = Caller();
        return SerializedAsync(GraphLock, async () =>
        {
            var placement = await VisibleAsync(caller, placementId, cancellationToken);
            await RequireManagerBelowAsync(caller, placement, cancellationToken);

            await RemoveTreeAsync(placement, cancellationToken);
            return true;
        });
    }

    // ---- Changing content -----------------------------------------------------------------

    public async Task<MediaItem> UpdateMetadataAsync(string placementId, MediaMetadata metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var caller = Caller();
        var placement = await VisibleAsync(caller, placementId, cancellationToken);
        await RequireAsync(caller, Edit, placement, cancellationToken);

        await SerializedAsync(ContentLock(placement.Placement.ContentId), async () =>
        {
            var document = await store.GetContentAsync(placement.Placement.ContentId) ?? throw new MediaObjectNotFoundException();
            document.Content.Metadata = metadata with { Tags = metadata.Tags is null ? null : [.. metadata.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)] };
            Touch(document, caller);
            await store.SaveAsync(document);

            // Tags are a fact rules read (requirement 78).
            await IndexContentAsync(document.Content.Id, cancellationToken);
            return true;
        });

        return await ItemAsync(placement);
    }

    public async Task<MediaItem> PublishVersionAsync(string placementId, int version, CancellationToken cancellationToken = default)
    {
        var caller = Caller();
        var placement = await VisibleAsync(caller, placementId, cancellationToken);
        await RequireAsync(caller, Edit, placement, cancellationToken);

        await SerializedAsync(ContentLock(placement.Placement.ContentId), async () =>
        {
            var document = await store.GetContentAsync(placement.Placement.ContentId) ?? throw new MediaObjectNotFoundException();
            if (document.Content.Versions.FirstOrDefault(candidate => candidate.Number == version) is not { State: MediaVersionState.Clean })
            {
                throw new MediaObjectConflictException("Only a clean version can be published.");
            }

            document.Content.PublishedVersion = version;
            Touch(document, caller);
            await store.SaveAsync(document);

            // The published version's type is a fact rules read.
            await IndexContentAsync(document.Content.Id, cancellationToken);
            return true;
        });

        return await ItemAsync(placement);
    }

    /// <summary>
    /// A final verdict from the quarantine (docs/media.md § 2): clean commits the version and
    /// publishes it when nothing is published yet; rejected keeps only the record and its reason;
    /// failed stays in quarantine until a later verdict. No caller: the verdict is the server's.
    /// </summary>
    public Task OnVerdictAsync(string contentId, int version, ScanVerdict verdict, CancellationToken cancellationToken = default) =>
        VerdictCoreAsync(contentId, version, verdict, cleanReason: null, cancellationToken);

    private Task VerdictCoreAsync(string contentId, int version, ScanVerdict verdict, string? cleanReason, CancellationToken cancellationToken) =>
        SerializedAsync(ContentLock(contentId), async () =>
        {
            var document = await store.GetContentAsync(contentId);
            var index = document?.Content.Versions.FindIndex(candidate => candidate.Number == version) ?? -1;
            if (document is null || index < 0)
            {
                logger.LogWarning("A scan verdict arrived for version {Version} of {ContentId}, which no longer exists.", version, contentId);
                return true;
            }

            var current = document.Content.Versions[index];
            if (current.State is MediaVersionState.Clean or MediaVersionState.Rejected)
            {
                return true;
            }

            switch (verdict.Outcome)
            {
                case ScanOutcome.Clean:
                    await storage.CommitAsync(current.BlobKey, cancellationToken);

                    // Scanning may rewrite the bytes (location data stripped), so size and hash
                    // are taken from what was committed, not from the upload.
                    var (size, sha256) = await MeasureAsync(current.BlobKey, cancellationToken) ?? (current.Size, current.Sha256);
                    document.Content.Versions[index] = current with { State = MediaVersionState.Clean, Size = size, Sha256 = sha256, DetectedType = verdict.DetectedType ?? current.DetectedType, Reason = cleanReason };
                    document.Content.PublishedVersion ??= version;
                    if (document.PublishWhenClean.Remove(version))
                    {
                        document.Content.PublishedVersion = version;
                    }

                    break;
                case ScanOutcome.Rejected:
                    document.PublishWhenClean.Remove(version);
                    await storage.DeleteAsync(MediaStorageArea.Quarantine, current.BlobKey, cancellationToken);
                    document.Content.Versions[index] = current with { State = MediaVersionState.Rejected, DetectedType = verdict.DetectedType ?? current.DetectedType, Reason = verdict.Reason };
                    break;
                case ScanOutcome.Failed:
                    document.Content.Versions[index] = current with { State = MediaVersionState.Failed, Reason = verdict.Reason };
                    break;
                default:
                    return true;
            }

            await store.SaveAsync(document);
            if (verdict.Outcome == ScanOutcome.Clean)
            {
                // The detected type is a fact rules read.
                await IndexContentAsync(contentId, cancellationToken);
            }

            return true;
        });

    // ---- Helpers --------------------------------------------------------------------------

    private CallerContext Caller() =>
        callers.Current ?? throw new AccessDeniedException(View, "File objects were asked for with no caller: the access gate did not run.");

    /// <summary>The decision for <paramref name="placement"/>; an inactive drive is out of
    /// reach for everyone but tenant administrators (requirement 23), sharing on or off.</summary>
    private async Task<bool> CanAsync(CallerContext caller, string permission, MediaPlacementDocument placement, CancellationToken cancellationToken)
    {
        var drive = await DriveOfAsync(placement);
        if (!caller.IsSuperUser && drive is { Content.IsInactive: true })
        {
            return false;
        }

        // A tenant administrator in another user's personal drive: only through an access in
        // force (requirement 24), and everything done there is recorded as administrator access.
        if (caller.IsSuperUser && drive?.Content is { DriveType: DriveType.Personal, Owner: { Kind: MediaOwnerKind.User } owner } && owner.Id != caller.UserId)
        {
            if (await personalAccess.CurrentAsync(placement.Placement.DriveId, cancellationToken) is not { } access)
            {
                return false;
            }

            foreach (var auditor in auditors)
            {
                await auditor.RecordAsync(new AccessEvent(
                    AccessEventKind.Execution,
                    permission,
                    caller,
                    AccessVerdict.Allow,
                    Resource: $"{FileShareableKind.Name}:{placement.Placement.Id}",
                    Reason: $"{PersonalDriveAccessService.AdministratorAccessReason} {access.Id}",
                    IsRead: permission == View), cancellationToken);
            }

            return true;
        }

        return await decision.AllowsAsync(caller, permission, placement.Placement, cancellationToken);
    }

    private async Task RequireAsync(CallerContext caller, string permission, MediaPlacementDocument placement, CancellationToken cancellationToken)
    {
        if (!await CanAsync(caller, permission, placement, cancellationToken))
        {
            throw new AccessDeniedException(permission, $"You may not do this to '{placement.Placement.Name}'.");
        }
    }

    /// <summary>Manager on the placement and on everything below it, including what the caller
    /// cannot see (requirements 69-70). The refusal never names or counts what failed.</summary>
    private async Task RequireManagerBelowAsync(CallerContext caller, MediaPlacementDocument placement, CancellationToken cancellationToken)
    {
        await RequireAsync(caller, Manage, await SelfAsync(placement), cancellationToken);
        if (placement.Placement.IsSoftLink)
        {
            return;
        }

        foreach (var below in await store.GetDescendantsAsync(placement.Placement.Id))
        {
            // A soft link below is the folder's to remove; its own parent is checked here too.
            if (!below.Placement.IsSoftLink && !await CanAsync(caller, Manage, below, cancellationToken))
            {
                throw new AccessDeniedException(Manage, "The folder contains items you cannot delete or move.");
            }
        }
    }

    private async Task<MediaPlacementDocument> VisibleAsync(CallerContext caller, string placementId, CancellationToken cancellationToken)
    {
        var placement = await store.GetPlacementAsync(placementId);
        if (placement is null || !await CanAsync(caller, View, placement, cancellationToken))
        {
            throw new MediaObjectNotFoundException();
        }

        return placement;
    }

    /// <summary>The placement a soft link opens (itself otherwise), visible to the caller.</summary>
    private async Task<MediaPlacementDocument> TargetAsync(MediaPlacementDocument placement, CallerContext caller, CancellationToken cancellationToken) =>
        placement.Placement.LinkTargetPlacementId is { } target ? await VisibleAsync(caller, target, cancellationToken) : placement;

    /// <summary>What changing the placement itself is checked against: a soft link's folder,
    /// since the link grants nothing and its target is not what changes.</summary>
    private async Task<MediaPlacementDocument> SelfAsync(MediaPlacementDocument placement) =>
        placement.Placement is { IsSoftLink: true, ParentId: { } parentId }
            ? await store.GetPlacementAsync(parentId) ?? throw new MediaObjectNotFoundException()
            : placement;

    /// <summary>A visible folder or drive to place something in (a soft link opens its target).</summary>
    private async Task<MediaPlacementDocument> ContainerAsync(CallerContext caller, string placementId, CancellationToken cancellationToken)
    {
        var placement = await TargetAsync(await VisibleAsync(caller, placementId, cancellationToken), caller, cancellationToken);
        var content = await store.GetContentAsync(placement.Placement.ContentId) ?? throw new MediaObjectNotFoundException();
        if (content.Content.Kind == MediaObjectKind.File)
        {
            throw new MediaObjectConflictException("Only a folder or a drive holds files.");
        }

        return placement;
    }

    private async Task<MediaContentDocument?> DriveOfAsync(MediaPlacementDocument placement)
    {
        var driveId = placement.Placement.DriveId;
        if (!_drives.TryGetValue(driveId, out var drive))
        {
            var drivePlacement = driveId == placement.Placement.Id ? placement : await store.GetPlacementAsync(driveId);
            drive = drivePlacement is null ? null : await store.GetContentAsync(drivePlacement.Placement.ContentId);
            _drives[driveId] = drive;
        }

        return drive;
    }

    /// <summary>Everything in a drive is owned by the drive's owner (requirement 14); in a
    /// personal drive that is its user, who owns what they create there.</summary>
    private async Task<MediaOwner> OwnerUnderAsync(MediaPlacementDocument parent) =>
        (await DriveOfAsync(parent))?.Content.Owner ?? MediaOwner.Tenant;

    private async Task<MediaItem> ItemAsync(MediaPlacementDocument placement)
    {
        var content = await store.GetContentAsync(placement.Placement.ContentId) ?? throw new MediaObjectNotFoundException();
        return new MediaItem(placement.Placement, content.Content);
    }

    private MediaContentDocument NewContent(MediaObjectKind kind, MediaOwner owner, CallerContext caller, string? id = null)
    {
        var now = Now();
        return new MediaContentDocument
        {
            Content = new MediaContent
            {
                Id = id ?? IdGenerator.GenerateId(),
                Kind = kind,
                Owner = owner,
                CreatedUtc = now,
                CreatedBy = caller.UserId,
                ModifiedUtc = now,
                ModifiedBy = caller.UserId,
            },
        };
    }

    private static MediaPlacementDocument NewPlacement(string contentId, MediaPlacementDocument parent, string name, MediaObjectKind kind) => new()
    {
        Kind = kind,
        Placement = new MediaPlacement
        {
            Id = IdGenerator.GenerateId(),
            ContentId = contentId,
            ParentId = parent.Placement.Id,
            DriveId = parent.Placement.DriveId,
            Name = name,
        },
    };

    private void Touch(MediaContentDocument document, CallerContext caller)
    {
        document.Content.ModifiedUtc = Now();
        document.Content.ModifiedBy = caller.UserId;
    }

    private DateTimeOffset Now() => new(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc));

    /// <summary>
    /// Removes one placement. The content goes with its last hard placement, and a folder's
    /// children with it (each through its own placement, so a child placed elsewhere too
    /// survives there). When the folder keeps another placement, the children placed through
    /// this one move to it. Soft links that open a removed placement go with it.
    /// </summary>
    private async Task RemoveAsync(MediaPlacementDocument placement, Dictionary<string, MediaPlacementDocument> recalculate, HashSet<string> removed)
    {
        if (!removed.Add(placement.Placement.Id))
        {
            return;
        }

        if (placement.Placement.IsSoftLink)
        {
            await store.DeleteAsync(placement);
            return;
        }

        var content = await store.GetContentAsync(placement.Placement.ContentId);
        var others = (await store.GetPlacementsOfContentAsync(placement.Placement.ContentId))
            .Where(other => other.Placement.Id != placement.Placement.Id && !removed.Contains(other.Placement.Id))
            .ToList();

        if (content is not null && content.Content.Kind != MediaObjectKind.File)
        {
            if (others.Count == 0)
            {
                foreach (var child in await store.GetChildrenAsync([placement.Placement.Id]))
                {
                    await RemoveAsync(child, recalculate, removed);
                }
            }
            else
            {
                // The folder stays; this path to it goes. Every child's closure loses it.
                foreach (var child in await store.GetChildrenAsync([.. others.Select(other => other.Placement.Id), placement.Placement.Id]))
                {
                    if (child.Placement.ParentId == placement.Placement.Id)
                    {
                        child.Placement.ParentId = others[0].Placement.Id;
                    }

                    recalculate[child.Placement.Id] = child;
                }
            }
        }

        foreach (var shortcut in await store.GetShortcutsToAsync(placement.Placement.Id))
        {
            removed.Add(shortcut.Placement.Id);
            await store.DeleteAsync(shortcut);
        }

        await store.DeleteAsync(placement);

        if (others.Count == 0 && content is not null)
        {
            foreach (var version in content.Content.Versions)
            {
                await storage.DeleteAsync(MediaStorageArea.Quarantine, version.BlobKey);
                await storage.DeleteAsync(MediaStorageArea.Committed, version.BlobKey);
            }

            await store.DeleteAsync(content);
        }
    }

    /// <summary>
    /// Takes the bytes of a new version into quarantine, hashing them on the way: through the
    /// registered <see cref="IUploadQuarantine"/>, which writes them to the Quarantine area under
    /// <see cref="MediaBlobKeys.ForVersion"/> and scans them after the request, or straight to the
    /// Quarantine area when no quarantine is registered.
    /// </summary>
    private async Task<MediaVersion> QuarantineAsync(string contentId, int number, string name, Stream content, string? claimedType, CallerContext caller, bool submit, CancellationToken cancellationToken)
    {
        var key = MediaBlobKeys.ForVersion(contentId, number);
        await using var hashing = new HashingStream(content);
        if (submit && quarantines.LastOrDefault() is { } quarantine)
        {
            await quarantine.SubmitAsync(contentId, number, name, claimedType, hashing, cancellationToken);
        }
        else
        {
            await storage.WriteAsync(MediaStorageArea.Quarantine, key, hashing, cancellationToken);
        }

        return new MediaVersion(number, key, hashing.Length, claimedType, null, hashing.Hash(), MediaVersionState.Quarantined, Now(), caller.UserId);
    }

    /// <summary>
    /// Requirement 86: no tenant runs without scanning. The scanning feature
    /// (<c>Crest.Media.Scanning</c>) registers the real quarantine; until it is on, a version is
    /// committed unscanned, and the log says so.
    /// </summary>
    private async Task CommitUnscannedAsync(string contentId, MediaVersion version, CancellationToken cancellationToken)
    {
        if (quarantines.Any())
        {
            return;
        }

        logger.LogError("Upload scanning is not configured: version {Version} of {ContentId} is committed without a scan.", version.Number, contentId);
        await OnVerdictAsync(contentId, version.Number, ScanVerdict.Clean, cancellationToken);
    }

    private async Task<(long Size, string Sha256)?> MeasureAsync(string key, CancellationToken cancellationToken)
    {
        await using var committed = await storage.OpenReadAsync(MediaStorageArea.Committed, key, cancellationToken);
        if (committed is null)
        {
            return null;
        }

        await using var hashing = new HashingStream(committed);
        await hashing.CopyToAsync(Stream.Null, cancellationToken);
        return (hashing.Length, hashing.Hash());
    }

    private static string ContentLock(string contentId) => $"media-files-content-{contentId}";

    /// <summary>Runs <paramref name="action"/> under the tenant's named lock and commits before
    /// releasing it, so the next holder reads what this one wrote.</summary>
    private async Task<T> SerializedAsync<T>(string key, Func<Task<T>> action)
    {
        var (locker, locked) = await locks.TryAcquireLockAsync(key, LockTimeout, LockExpiration);
        if (!locked)
        {
            throw new TimeoutException("The file system is busy; try again.");
        }

        await using (locker)
        {
            var result = await action();
            await store.CommitAsync();
            return result;
        }
    }

    /// <summary>Counts and hashes what is read through it.</summary>
    private sealed class HashingStream(Stream inner) : Stream
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long _length;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _length;

        public override long Position { get => _length; set => throw new NotSupportedException(); }

        public string Hash() => Convert.ToHexStringLower(_hash.GetHashAndReset());

        public override int Read(byte[] buffer, int offset, int count) => Count(buffer.AsSpan(offset, inner.Read(buffer, offset, count)));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            return Count(buffer.Span[..read]);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        private int Count(ReadOnlySpan<byte> read)
        {
            _hash.AppendData(read);
            _length += read.Length;
            return read.Length;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hash.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
