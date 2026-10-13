using Crest.Access;
using Crest.Modules;
using Crest.Sharing;
using Crest.Sharing.Services;

namespace Crest.Tests.Sharing;

/// <summary>
/// The fixtures of the sharing tests: an in-memory store, a test kind ("file") whose objects
/// form a tree, a settable clock, and the example drive of docs/sharing.md › Example.
/// </summary>
public sealed class SharingWorld
{
    public const string Kind = "file";

    public MemoryStore Store { get; } = new();

    public TestKind Files { get; } = new();

    public TestClock Clock { get; } = new();

    public ShareableKinds Kinds() => new([Files]);

    public SharingResolver Resolver() => new(Kinds(), Store, Clock);

    public MemoryAccessStore AccessStore { get; } = new();

    /// <summary>The access index over <see cref="AccessStore"/> (or another store: SQLite in the
    /// list tests).</summary>
    public ShareAccessIndexer Indexer(IShareAccessStore store = null) => new(Kinds(), Resolver(), store ?? AccessStore, Clock);

    /// <summary>Calculates every object of the tree into the access index.</summary>
    public async Task IndexAllAsync(IShareAccessStore store = null)
    {
        var indexer = Indexer(store);
        foreach (var id in Files.Nodes.Keys)
        {
            await indexer.RecalculateAsync(Ref(id));
        }
    }

    /// <summary>The access index documents in memory, with the time registry and the queue.</summary>
    public sealed class MemoryAccessStore : IShareAccessStore
    {
        public Dictionary<ShareableRef, ShareAccessDocument> Documents { get; } = [];

        public List<ShareRecalculation> Queue { get; } = [];

        private readonly List<ShareableRef> _order = [];

        /// <summary>The documents of paths other than the primary one (multi-path inheritance).</summary>
        public Dictionary<(ShareableRef Ref, string PathKey), ShareAccessDocument> OtherPaths { get; } = [];

        public Task<ShareAccessDocument> GetAsync(ShareableRef target) => Task.FromResult(Documents.GetValueOrDefault(target));

        public Task<IReadOnlyList<ShareAccessDocument>> GetPathsAsync(ShareableRef target) =>
            Task.FromResult<IReadOnlyList<ShareAccessDocument>>([
                .. Documents.TryGetValue(target, out var primary) ? [primary] : Array.Empty<ShareAccessDocument>(),
                .. OtherPaths.Where(pair => pair.Key.Ref == target).Select(pair => pair.Value)]);

        public Task SaveAsync(ShareAccessDocument document)
        {
            if (document.PathKey.Length > 0)
            {
                OtherPaths[(document.Ref, document.PathKey)] = document;
                return Task.CompletedTask;
            }

            if (!Documents.ContainsKey(document.Ref))
            {
                _order.Add(document.Ref);
            }

            Documents[document.Ref] = document;
            return Task.CompletedTask;
        }

        public async Task SavePathsAsync(ShareableRef target, IReadOnlyList<ShareAccessDocument> documents)
        {
            foreach (var document in documents)
            {
                await SaveAsync(document);
            }

            foreach (var gone in OtherPaths.Keys.Where(key => key.Ref == target && documents.All(document => document.PathKey != key.PathKey)).ToList())
            {
                OtherPaths.Remove(gone);
            }
        }

        public Task DeleteAsync(ShareableRef target)
        {
            Documents.Remove(target);
            _order.Remove(target);
            foreach (var gone in OtherPaths.Keys.Where(key => key.Ref == target).ToList())
            {
                OtherPaths.Remove(gone);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ShareableRef>> DueAsync(DateTime utc, int max) =>
            Task.FromResult<IReadOnlyList<ShareableRef>>([.. Documents.Values
                .Where(document => document.NextBoundaryUtc <= utc)
                .OrderBy(document => document.NextBoundaryUtc)
                .Take(max)
                .Select(document => document.Ref)]);

        public Task<(IReadOnlyList<ShareableRef> Page, long Cursor)> PageAsync(long afterId, int max)
        {
            var page = _order.Skip((int)afterId).Take(max).ToList();
            return Task.FromResult<(IReadOnlyList<ShareableRef>, long)>((page, afterId + page.Count));
        }

        public Task<IReadOnlyList<ShareableRef>> FindGrantedAsync(IReadOnlyCollection<SharePrincipal> principals, DateTime utc) =>
            Task.FromResult<IReadOnlyList<ShareableRef>>([.. Documents.Values
                .Where(document => document.Rows.Any(row =>
                    row.IsDirect && row.Admits && row.IsActiveAt(utc) &&
                    SharePrincipals.Matches([.. principals], new SharePrincipal(row.PrincipalKind, row.PrincipalKind is SharePrincipalKind.Anyone or SharePrincipalKind.Tenant ? null : row.PrincipalId))))
                .Select(document => document.Ref)]);

        public Task QueueAsync(ShareableRef subtree, DateTime utc)
        {
            if (!Queue.Any(queued => queued.Ref == subtree))
            {
                Queue.Add(new ShareRecalculation { Kind = subtree.Kind, ObjectId = subtree.Id, RequestedUtc = utc });
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ShareRecalculation>> QueuedAsync(int max) => Task.FromResult<IReadOnlyList<ShareRecalculation>>([.. Queue.Take(max)]);

        public Task DequeueAsync(ShareRecalculation recalculation)
        {
            Queue.Remove(recalculation);
            return Task.CompletedTask;
        }

        public Task FlushAsync() => Task.CompletedTask;

        public Task CommitAsync() => Task.CompletedTask;
    }

    public sealed class MemoryStore : IShareStore
    {
        public Dictionary<ShareableRef, ShareDocument> Documents { get; } = [];

        public Task<ShareDocument?> GetAsync(ShareableRef target) => Task.FromResult(Documents.GetValueOrDefault(target));

        public Task SaveAsync(ShareDocument document)
        {
            Documents[document.Ref] = document;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ShareableRef>> FindGrantedAsync(IReadOnlyCollection<SharePrincipal> principals) =>
            Task.FromResult<IReadOnlyList<ShareableRef>>([.. Documents.Values
                .Where(document => document.Entries.Any(entry => !entry.IsBlock && principals.Contains(entry.Principal)))
                .Select(document => document.Ref)]);
    }

    /// <summary>A file tree: drives, folders and files by id, with a parent and facts.</summary>
    public sealed class TestKind : IShareableKind, IShareableTree, IShareableScopeTarget, IShareableRoots, IShareableAdminAssignment
    {
        /// <summary>The tenant permission that assigns Admins on roots (drives).</summary>
        public const string ManageDrives = "ManageSharedDrives";

        public string AdminAssignmentPermission => ManageDrives;

        public Task<IReadOnlyList<ShareableRef>> RootsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ShareableRef>>([.. Nodes.Values.Where(node => node.ParentId is null).Select(node => new ShareableRef(Kind, node.Id))]);

        /// <summary>Where the list tests keep the files: one row per file.</summary>
        public ShareScopeTarget ScopeTarget { get; } = new("SharedFileIndex", "FileId");

        public Task<IReadOnlyList<ShareableRef>> ChildrenAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ShareableRef>>([.. Nodes.Values.Where(node => node.ParentId == id).Select(node => new ShareableRef(Kind, node.Id))]);

        public sealed record Node(string Id, string? ParentId, string? Owner = null, bool Inactive = false, Dictionary<string, IReadOnlyList<string>>? Facts = null, string? OwnerAccount = null);

        public Dictionary<string, Node> Nodes { get; } = new(StringComparer.Ordinal);

        public string Name => Kind;

        public bool TryIdentify(object resource, out string id)
        {
            if (resource is Node node)
            {
                id = node.Id;
                return true;
            }

            id = null;
            return false;
        }

        public Task<ShareableObject> GetAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Nodes.TryGetValue(id, out var node)
                ? new ShareableObject
                {
                    Ref = new ShareableRef(Kind, node.Id),
                    Parent = node.ParentId is null ? null : new ShareableRef(Kind, node.ParentId),
                    OwnerUserId = node.Owner,
                    OwnerAccountId = node.OwnerAccount,
                    IsInactive = node.Inactive,
                    Facts = node.Facts ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                }
                : null);

        public AccessLevel? LevelFor(string permission) => permission switch
        {
            "ViewMedia" => AccessLevel.Viewer,
            "CommentMedia" => AccessLevel.Commenter,
            "EditMedia" => AccessLevel.Editor,
            "DeleteMedia" => AccessLevel.Manager,
            "ShareMedia" => AccessLevel.Admin,
            _ => null,
        };

        public Node Add(string id, string parent = null, string owner = null, bool inactive = false, string extension = null, string tag = null, string ownerAccount = null)
        {
            var facts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            if (extension is not null)
            {
                facts["extension"] = [extension];
            }

            if (tag is not null)
            {
                facts["tag"] = [tag];
            }

            return Nodes[id] = new Node(id, parent, owner, inactive, facts, ownerAccount);
        }
    }

    public sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

        public ITimeZone[] GetTimeZones() => [];

        public ITimeZone GetTimeZone(string timeZoneId) => null;

        public ITimeZone GetSystemTimeZone() => null;

        public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => dateTimeOffset;
    }

    public static ShareableRef Ref(string id) => new(Kind, id);

    public void Share(string id, params ShareEntry[] entries)
    {
        var target = id is null ? ShareableRef.TenantRoot : Ref(id);
        var document = Store.Documents.GetValueOrDefault(target) ?? new ShareDocument { Kind = target.Kind, ObjectId = target.Id };
        document.Entries.AddRange(entries);
        Store.Documents[target] = document;
    }

    public void Rule(string id, ShareRule rule)
    {
        var target = id is null ? ShareableRef.TenantRoot : Ref(id);
        var document = Store.Documents.GetValueOrDefault(target) ?? new ShareDocument { Kind = target.Kind, ObjectId = target.Id };
        document.Rules.Add(rule);
        Store.Documents[target] = document;
    }

    public static CallerContext User(string userId, params string[] roles) => new()
    {
        Tenant = "Default",
        Side = CallerSide.Admin,
        UserId = userId,
        UserName = userId,
        Roles = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase),
    };

    public static CallerContext Member(string userId, string organizationId, params string[] roles) => new()
    {
        Tenant = "Default",
        Side = CallerSide.Member,
        UserId = userId,
        UserName = userId,
        OrganizationId = organizationId,
        UserClass = CallerClasses.Member,
        Roles = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase),
    };

    public static readonly CallerContext Visitor = CallerContext.Anonymous("Default", CallerSide.Site);

    /// <summary>
    /// The example of docs/sharing.md: Product Info (a shared drive owned by the tenant account)
    /// grants Customer and Staff and blocks Sam; Spec sheets is open to the tenant; Images adds
    /// Marketing as Editor, with rule 1 (Photoshop files: block Customer, Dana edits) and rule 2
    /// (tag "final": public).
    /// </summary>
    public static SharingWorld Example()
    {
        var world = new SharingWorld();
        world.Files.Add("product-info");
        world.Files.Add("spec-sheets", "product-info");
        world.Files.Add("spec.psd", "spec-sheets", extension: "psd");
        world.Files.Add("images", "product-info");
        world.Files.Add("chair-original.jpg", "images", extension: "jpg");
        world.Files.Add("chair.psd", "images", extension: "psd");
        world.Files.Add("chair-final.jpg", "images", extension: "jpg", tag: "final");

        world.Share("product-info", ShareEntry.Grant(SharePrincipal.Role("Customer"), AccessLevel.Viewer), ShareEntry.Grant(SharePrincipal.Role("Staff"), AccessLevel.Viewer), ShareEntry.Block(SharePrincipal.User("sam")));
        world.Share("spec-sheets", ShareEntry.Grant(SharePrincipal.Tenant, AccessLevel.Viewer));
        world.Share("images", ShareEntry.Grant(SharePrincipal.Role("Marketing"), AccessLevel.Editor));
        world.Rule("images", new ShareRule(
            "rule-1",
            "Photoshop files",
            [new ShareCondition("extension", ShareConditionOperator.Equals, ["psd"])],
            [ShareEntry.Block(SharePrincipal.Role("Customer")), ShareEntry.Grant(SharePrincipal.User("dana"), AccessLevel.Editor)]));
        world.Rule("images", new ShareRule(
            "rule-2",
            "Final images are public",
            [new ShareCondition("tag", ShareConditionOperator.Equals, ["final"])],
            [ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Viewer)]));
        return world;
    }
}
