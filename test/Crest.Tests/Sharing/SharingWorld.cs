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
    public sealed class TestKind : IShareableKind
    {
        public sealed record Node(string Id, string? ParentId, string? Owner = null, bool Inactive = false, Dictionary<string, IReadOnlyList<string>>? Facts = null);

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

        public Node Add(string id, string parent = null, string owner = null, bool inactive = false, string extension = null, string tag = null)
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

            return Nodes[id] = new Node(id, parent, owner, inactive, facts);
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
