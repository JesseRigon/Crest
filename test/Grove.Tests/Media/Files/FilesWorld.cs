#nullable enable
using Grove.Access;
using Grove.Access.Services;
using Grove.Locking;
using Grove.Locking.Distributed;
using Grove.Media;
using Grove.Media.Files;
using DriveType = Grove.Media.Files.DriveType;
using Grove.Media.Sharing;
using Grove.Access.Sharing;
using Grove.Access.Sharing.Services;
using Grove.Tests.Access.Conformance;
using Grove.Tests.Sharing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Grove.Tests.Media.Files;

/// <summary>
/// The file-object tests' fixtures: an in-memory placement store and byte storage, the real
/// access decision with sharing as the authority over the "file" kind, and callers. A shared
/// drive "Drive" is created by a tenant administrator; everyone else gets only what is shared.
/// </summary>
public sealed class FilesWorld
{
    public MemoryObjectStore Store { get; } = new();

    public MemoryStorage Storage { get; } = new();

    public SharingWorld Sharing { get; } = new();

    public FileDriveSettings Settings { get; } = new() { PersonalDrives = true, OrganizationDrives = true };

    public RecordingQuarantine? Quarantine { get; set; }

    public PersonalDriveAccessSettings PersonalDriveAccess { get; } = new();

    public MemoryAccessStore Accesses { get; } = new();

    public RecordingNotifier Notifier { get; } = new();

    public AccessWorld.RecordingAuditor Audit { get; } = new();

    /// <summary>The access index and scope, when a test runs with sharing's lists on.</summary>
    public IShareAccessIndex? AccessIndex { get; set; }

    public IShareScopes? Scopes { get; set; }

    public IMediaObjectStore? StoreOverride { get; set; }

    /// <summary>Sharing's service for <paramref name="caller"/>, over the world's stores.</summary>
    public SharingService SharingService(CallerContext? caller) =>
        new(new Accessor(caller), Decision(), Resolver(), Sharing.Store, new ShareableKinds([Kind]), Audit);

    /// <summary>Whether services see sharing's service (organization drives grant through it).</summary>
    public bool WithSharingService { get; set; }

    public MediaFileShareableKind Kind => new(StoreOverride ?? Store);

    public static readonly CallerContext Root = new() { Tenant = "Default", Side = CallerSide.Admin, UserId = "root", UserName = "root", IsSuperUser = true };

    public static CallerContext User(string userId, params string[] roles) => SharingWorld.User(userId, roles);

    public ISharingResolver Resolver()
    {
        var kinds = new ShareableKinds([Kind]);
        return new SharingResolver(kinds, Sharing.Store, Sharing.Clock);
    }

    public IAccessDecision Decision()
    {
        var kinds = new ShareableKinds([Kind]);
        return new AccessDecisionService(
            new AccessWorld.PermissionRegistry(),
            [],
            [new SharingPermissionMapper([Kind], kinds, new SharingResolver(kinds, Sharing.Store, Sharing.Clock))],
            new AccessWorld.RecordingAuditor());
    }

    public MediaObjectService Service(CallerContext? caller)
    {
        var store = StoreOverride ?? Store;
        var accessor = new Accessor(caller);
        var decision = Decision();
        return new(
            store,
            new MediaGraph(store),
            Storage,
            decision,
            accessor,
            new NoLock(),
            Sharing.Clock,
            new SettingsAccessor(Settings, PersonalDriveAccess),
            Quarantine is null ? [] : [Quarantine],
            NullLogger<MediaObjectService>.Instance,
            PersonalAccess(caller),
            [Audit],
            AccessIndex is null ? [] : [AccessIndex],
            Scopes is null ? [] : [Scopes],
            WithSharingService ? [SharingService(caller)] : [],
            [new ShareChangeChecks(accessor, Resolver(), new ShareableKinds([Kind]))]);
    }

    public PersonalDriveAccessService PersonalAccess(CallerContext? caller) =>
        new(Accesses, StoreOverride ?? Store, new Accessor(caller), Decision(), Sharing.Clock, new SettingsAccessor(Settings, PersonalDriveAccess), Notifier, [Audit]);

    public MediaGraph Graph() => new(Store);

    public void Share(string placementId, params ShareEntry[] entries)
    {
        var target = new ShareableRef(MediaFileShareableKind.KindName, placementId);
        var document = Sharing.Store.Documents.GetValueOrDefault(target) ?? new ShareDocument { Kind = target.Kind, ObjectId = target.Id };
        document.Entries.AddRange(entries);
        Sharing.Store.Documents[target] = document;
    }

    public static MediaUpload Upload(string parentId, string name, string text = "bytes", string type = "text/plain") =>
        new(parentId, name, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)), type);

    /// <summary>A shared drive made by the tenant administrator.</summary>
    public async Task<MediaItem> DriveAsync(string name = "Drive") =>
        await Service(Root).CreateDriveAsync(name, DriveType.Shared, MediaOwner.Tenant, TestContext.Current.CancellationToken);

    public sealed class Accessor(CallerContext? current) : ICallerContextAccessor
    {
        public CallerContext? Current { get; set; } = current;
    }

    public sealed class SettingsAccessor(FileDriveSettings settings, PersonalDriveAccessSettings personal) : IFileDriveSettingsAccessor
    {
        public Task<FileDriveSettings> GetAsync() => Task.FromResult(settings);

        public Task<PersonalDriveAccessSettings> GetPersonalDriveAccessAsync() => Task.FromResult(personal);

        public Task SaveAsync(FileDriveSettings? drives, PersonalDriveAccessSettings? personalDriveAccess) => Task.CompletedTask;
    }

    public sealed class MemoryAccessStore : IPersonalDriveAccessStore
    {
        public Dictionary<string, PersonalDriveAccessDocument> Documents { get; } = new(StringComparer.Ordinal);

        public Task<PersonalDriveAccessDocument?> GetAsync(string accessId) => Task.FromResult(Documents.GetValueOrDefault(accessId));

        public Task<IReadOnlyList<PersonalDriveAccessDocument>> ListOpenAsync(string driveId, string administratorUserId) =>
            Task.FromResult<IReadOnlyList<PersonalDriveAccessDocument>>([.. Documents.Values.Where(document =>
                document.Access.DriveId == driveId && document.Access.AdministratorUserId == administratorUserId && document.Access.ClosedUtc is null)]);

        public Task SaveAsync(PersonalDriveAccessDocument document)
        {
            Documents[document.Access.Id] = document;
            return Task.CompletedTask;
        }
    }

    /// <summary>Records notices; <see cref="Fails"/> makes every one fail.</summary>
    public sealed class RecordingNotifier : IPersonalDriveNotifier
    {
        public bool Fails { get; set; }

        public List<(string Owner, PersonalDriveNotice Notice, string Body)> Sent { get; } = [];

        public Task<(bool Sent, string Detail)> NotifyAsync(string ownerUserId, PersonalDriveNotice notice, string subject, string body, CancellationToken cancellationToken = default)
        {
            if (Fails)
            {
                return Task.FromResult((false, "failed: email (not configured)"));
            }

            Sent.Add((ownerUserId, notice, body));
            return Task.FromResult((true, "sent by email"));
        }
    }

    public sealed class NoLock : IDistributedLock
    {
        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null) => Task.FromResult<ILocker>(new Locker());

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null) => Task.FromResult<(ILocker, bool)>((new Locker(), true));

        public Task<bool> IsLockAcquiredAsync(string key) => Task.FromResult(false);

        private sealed class Locker : ILocker
        {
            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>A quarantine that only takes the bytes (as the scanning feature's does) and
    /// records what it was handed; verdicts are given by the test.</summary>
    public sealed class RecordingQuarantine(MemoryStorage storage) : IUploadQuarantine
    {
        public List<(string ContentId, int Version)> Submitted { get; } = [];

        public async Task<string> SubmitAsync(string contentId, int version, string name, string? claimedType, Stream content, CancellationToken cancellationToken = default)
        {
            await storage.WriteAsync(MediaStorageArea.Quarantine, MediaBlobKeys.ForVersion(contentId, version), content, cancellationToken);
            Submitted.Add((contentId, version));
            return $"{contentId}/{version}";
        }

        public Task ReportVerdictAsync(string scanId, string scannerName, ScanVerdict verdict, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public sealed class MemoryStorage : IMediaStorageProvider
    {
        public Dictionary<(MediaStorageArea Area, string Key), byte[]> Blobs { get; } = [];

        public string Name => "memory";

        public async Task WriteAsync(MediaStorageArea area, string key, Stream content, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Blobs[(area, key)] = buffer.ToArray();
        }

        public Task<Stream?> OpenReadAsync(MediaStorageArea area, string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(Blobs.TryGetValue((area, key), out var bytes) ? new MemoryStream(bytes) : null);

        public Task CommitAsync(string key, CancellationToken cancellationToken = default)
        {
            if (Blobs.Remove((MediaStorageArea.Quarantine, key), out var bytes))
            {
                Blobs[(MediaStorageArea.Committed, key)] = bytes;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(MediaStorageArea area, string key, CancellationToken cancellationToken = default)
        {
            Blobs.Remove((area, key));
            return Task.CompletedTask;
        }

        public Task<Uri?> GetTemporaryReadUriAsync(MediaStorageArea area, string key, TimeSpan lifetime, CancellationToken cancellationToken = default) => Task.FromResult<Uri?>(null);
    }

    /// <summary>The store's queries over dictionaries, the closure read from the documents.</summary>
    public sealed class MemoryObjectStore : IMediaObjectStore
    {
        public Dictionary<string, MediaContentDocument> Contents { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, MediaPlacementDocument> Placements { get; } = new(StringComparer.Ordinal);

        public int Commits { get; private set; }

        public Task<MediaContentDocument?> GetContentAsync(string contentId) => Task.FromResult(Contents.GetValueOrDefault(contentId));

        public Task<IReadOnlyList<MediaContentDocument>> GetContentsAsync(IReadOnlyCollection<string> contentIds) =>
            Task.FromResult<IReadOnlyList<MediaContentDocument>>([.. contentIds.Distinct().Select(Contents.GetValueOrDefault).OfType<MediaContentDocument>()]);

        public Task<MediaContentDocument?> GetPersonalDriveAsync(string userId) =>
            Task.FromResult(Contents.Values.FirstOrDefault(content => content.Content.DriveType == DriveType.Personal && content.Content.Owner.Id == userId));

        public Task<MediaPlacementDocument?> GetPlacementAsync(string placementId) => Task.FromResult(Placements.GetValueOrDefault(placementId));

        public Task<IReadOnlyList<MediaPlacementDocument>> GetPlacementsOfContentAsync(string contentId) =>
            Where(placement => placement.Placement.ContentId == contentId && !placement.Placement.IsSoftLink);

        public Task<IReadOnlyList<MediaPlacementDocument>> GetChildrenAsync(IReadOnlyCollection<string> parentPlacementIds) =>
            Where(placement => placement.Placement.ParentId is { } parent && parentPlacementIds.Contains(parent));

        public Task<IReadOnlyList<MediaPlacementDocument>> GetChildrenNamedAsync(IReadOnlyCollection<string> parentPlacementIds, string name) =>
            Where(placement => placement.Placement.ParentId is { } parent && parentPlacementIds.Contains(parent) && string.Equals(placement.Placement.Name, name, StringComparison.OrdinalIgnoreCase));

        public Task<IReadOnlyList<MediaPlacementDocument>> GetDescendantsAsync(string ancestorPlacementId) =>
            Where(placement => placement.Ancestors.Any(ancestor => ancestor.PlacementId == ancestorPlacementId));

        public Task<IReadOnlyList<MediaPlacementDocument>> GetShortcutsToAsync(string targetPlacementId) =>
            Where(placement => placement.Placement.LinkTargetPlacementId == targetPlacementId);

        public Task<bool> HasAncestorContentAsync(string placementId, string contentId) =>
            Task.FromResult(Placements.GetValueOrDefault(placementId)?.Ancestors.Any(ancestor => ancestor.ContentId == contentId) == true);

        public Task<IReadOnlyList<MediaPlacementDocument>> GetDrivesAsync() =>
            Where(placement => placement.Placement.ParentId is null && !placement.Placement.IsSoftLink);

        public Task<(IReadOnlyList<MediaPlacementDocument> Page, int Total)> GetChildrenAsync(IReadOnlyCollection<string> parentPlacementIds, ScopeRule scope, int skip, int take)
        {
            if (scope.Kind == ScopeKind.Filter)
            {
                throw new NotSupportedException("The memory store cannot apply sharing's scope; use the SQLite store.");
            }

            var all = scope.Kind == ScopeKind.None
                ? []
                : Placements.Values.Where(placement => placement.Placement.ParentId is { } parent && parentPlacementIds.Contains(parent)).ToList();
            return Task.FromResult<(IReadOnlyList<MediaPlacementDocument>, int)>(([.. all.Skip(skip).Take(take)], all.Count));
        }

        public Task<IReadOnlyList<MediaPlacementDocument>> GetDrivesAsync(ScopeRule scope) =>
            scope.Kind == ScopeKind.Filter ? throw new NotSupportedException() : scope.Kind == ScopeKind.None ? Task.FromResult<IReadOnlyList<MediaPlacementDocument>>([]) : GetDrivesAsync();

        public Task<IReadOnlyList<MediaContentDocument>> GetDriveContentsAsync(DriveType type) =>
            Task.FromResult<IReadOnlyList<MediaContentDocument>>([.. Contents.Values.Where(content => content.Content.DriveType == type)]);

        public Task<MediaContentDocument?> GetOrganizationDriveAsync(string organizationId) =>
            Task.FromResult(Contents.Values.FirstOrDefault(content => content.Content.DriveType == DriveType.Organization && content.Content.Owner == MediaOwner.Organization(organizationId)));

        public Task SaveAsync(MediaContentDocument document)
        {
            Contents[document.Content.Id] = document;
            return Task.CompletedTask;
        }

        public Task SaveAsync(MediaPlacementDocument document)
        {
            Placements[document.Placement.Id] = document;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(MediaContentDocument document)
        {
            Contents.Remove(document.Content.Id);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(MediaPlacementDocument document)
        {
            Placements.Remove(document.Placement.Id);
            return Task.CompletedTask;
        }

        public Task CommitAsync()
        {
            Commits++;
            return Task.CompletedTask;
        }

        private Task<IReadOnlyList<MediaPlacementDocument>> Where(Func<MediaPlacementDocument, bool> predicate) =>
            Task.FromResult<IReadOnlyList<MediaPlacementDocument>>([.. Placements.Values.Where(predicate)]);
    }
}
