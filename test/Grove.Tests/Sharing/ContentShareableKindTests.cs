using Grove.Access;
using Grove.Contents.Management;
using Grove.Contents.Security;
using Grove.Contents.Sharing;
using Grove.Access.Sharing;
using Grove.Access.Sharing.Services;
using static Grove.Tests.Sharing.SharingWorld;

namespace Grove.Tests.Sharing;

/// <summary>
/// Content items and content types as shareable kinds (docs/sharing.md § 6, requirement 73):
/// the chain tenant → type → item, the permission levels, creation deciding on the type, the
/// content list scope yielding to sharing, and requirement 84's grants.
/// </summary>
public class ContentShareableKindTests
{
    private sealed class Lookup : IContentShareLookup
    {
        public Dictionary<string, (string Type, string Owner)> Items { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Types { get; } = new(StringComparer.Ordinal) { "Article", "Invoice" };

        public Task<(string ContentType, string Owner)?> ItemAsync(string contentItemId) =>
            Task.FromResult<(string, string)?>(Items.TryGetValue(contentItemId, out var item) ? (item.Type, item.Owner) : null);

        public Task<IReadOnlyList<string>> ItemsOfTypeAsync(string contentType) =>
            Task.FromResult<IReadOnlyList<string>>([.. Items.Where(item => item.Value.Type == contentType).Select(item => item.Key)]);

        Task<IReadOnlyList<string>> IContentShareLookup.TypesAsync() => Task.FromResult<IReadOnlyList<string>>([.. Types]);

        public Task<bool> TypeExistsAsync(string contentType) => Task.FromResult(Types.Contains(contentType));
    }

    private sealed class Store : IShareStore
    {
        public Dictionary<ShareableRef, ShareDocument> Documents { get; } = [];

        public Task<ShareDocument> GetAsync(ShareableRef target) => Task.FromResult(Documents.GetValueOrDefault(target));

        public Task SaveAsync(ShareDocument document)
        {
            Documents[document.Ref] = document;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ShareableRef>> FindGrantedAsync(IReadOnlyCollection<SharePrincipal> principals) => Task.FromResult<IReadOnlyList<ShareableRef>>([]);

        public void Share(ShareableRef target, params ShareEntry[] entries) =>
            Documents[target] = new ShareDocument { Kind = target.Kind, ObjectId = target.Id, Entries = [.. entries] };
    }

    private static readonly ShareableRef s_article = new(ContentTypeShareableKind.KindName, "Article");

    private static (Lookup Lookup, ContentShareableKind Items, IShareableKind[] Kinds, Store Store, SharingResolver Resolver) World()
    {
        var lookup = new Lookup();
        lookup.Items["a1"] = ("Article", "olga");
        lookup.Items["i1"] = ("Invoice", "olga");
        var items = new ContentShareableKind(lookup);
        IShareableKind[] kinds = [new ContentTypeShareableKind(lookup), items];
        var store = new Store();
        return (lookup, items, kinds, store, new SharingResolver(new ShareableKinds(kinds), store, new TestClock()));
    }

    [Theory]
    [InlineData("ViewContent", AccessLevel.Viewer)]
    [InlineData("ViewOwnContent", AccessLevel.Viewer)]
    [InlineData("View_Article", AccessLevel.Viewer)]
    [InlineData("PreviewContent", AccessLevel.Editor)]
    [InlineData("PreviewOwn_Article", AccessLevel.Editor)]
    [InlineData("EditContent", AccessLevel.Editor)]
    [InlineData("EditOwn_Article", AccessLevel.Editor)]
    [InlineData("PublishContent", AccessLevel.Manager)]
    [InlineData("Publish_Article", AccessLevel.Manager)]
    [InlineData("DeleteOwnContent", AccessLevel.Manager)]
    [InlineData("CloneContent", null)]
    [InlineData("ListContent", null)]
    [InlineData("EditContentOwner", null)]
    [InlineData("ViewMedia", null)]
    public void ContentPermissionsTakeLevels(string permission, AccessLevel? level) =>
        Assert.Equal(level, ContentShareLevels.LevelFor(permission));

    [Fact]
    public async Task AnItemInheritsItsTypesEntriesAndItsOwnerManagesIt()
    {
        var (_, _, _, store, resolver) = World();
        store.Share(s_article, ShareEntry.Grant(SharePrincipal.Role("Customer"), AccessLevel.Viewer));
        var a1 = new ShareableRef(ContentShareableKind.KindName, "a1");
        var i1 = new ShareableRef(ContentShareableKind.KindName, "i1");

        Assert.Equal(AccessLevel.Viewer, (await resolver.ResolveAsync(User("cora", "Customer"), a1, TestContext.Current.CancellationToken)).Level);
        Assert.Equal(AccessLevel.None, (await resolver.ResolveAsync(User("cora", "Customer"), i1, TestContext.Current.CancellationToken)).Level);
        Assert.Equal(AccessLevel.Manager, (await resolver.ResolveAsync(User("olga"), i1, TestContext.Current.CancellationToken)).Level);

        var chain = await resolver.ChainAsync(a1, TestContext.Current.CancellationToken);
        Assert.Equal([s_article, a1], chain!.Select(item => item.Ref));
    }

    [Fact]
    public async Task CreatingAnItemDecidesOnItsType()
    {
        var (_, _, kinds, store, resolver) = World();
        var mapper = new SharingPermissionMapper(kinds, new ShareableKinds(kinds), resolver);
        var draft = new ContentItem { ContentItemId = "new", ContentType = "Article", Owner = "wes" };

        // The creator is set as owner, but an item that does not exist yet has no owner.
        Assert.Empty((await mapper.MapAsync("EditContent", draft, User("wes"), TestContext.Current.CancellationToken))!);

        store.Share(s_article, ShareEntry.Grant(SharePrincipal.Role("Writer"), AccessLevel.Editor));
        Assert.Equal([PermissionCandidate.Granted], await mapper.MapAsync("EditContent", draft, User("wes", "Writer"), TestContext.Current.CancellationToken));

        // Asked against the type's name: the type kind answers, not a media path kind.
        Assert.Equal([PermissionCandidate.Granted], await mapper.MapAsync("EditContent", "Article", User("wes", "Writer"), TestContext.Current.CancellationToken));

        // Permissions sharing does not map stay with the content mapper.
        Assert.Null(await mapper.MapAsync("CloneContent", draft, User("wes", "Writer"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ASavedItemsOwnerCountsBeforeTheIndexHasIt()
    {
        var (_, items, _, _, resolver) = World();
        items.Remember(new ContentItem { ContentItemId = "fresh", ContentType = "Article", Owner = "wes" });

        Assert.Equal(AccessLevel.Manager, (await resolver.ResolveAsync(User("wes"), new ShareableRef(ContentShareableKind.KindName, "fresh"), TestContext.Current.CancellationToken)).Level);
    }

    [Fact]
    public async Task TheTypeIsTheRootItsItemsAreItsChildren()
    {
        var (lookup, _, kinds, _, _) = World();
        var types = (ContentTypeShareableKind)kinds[0];

        Assert.Equal([new ShareableRef(ContentShareableKind.KindName, "a1")], await types.ChildrenAsync("Article", TestContext.Current.CancellationToken));
        Assert.Equal(lookup.Types.Count, (await types.RootsAsync(TestContext.Current.CancellationToken)).Count);
        Assert.True((await types.GetAsync("Article", TestContext.Current.CancellationToken))!.IsRoot);
        Assert.Null(await types.GetAsync("Missing", TestContext.Current.CancellationToken));
    }

    private sealed class Scopes(ScopeRule rule) : IShareScopes
    {
        public AccessLevel? Asked { get; private set; }

        public ScopeRule RuleFor(CallerContext caller, string kind, AccessLevel minimum)
        {
            Asked = minimum;
            return kind == ContentShareableKind.KindName ? rule : null;
        }
    }

    [Fact]
    public async Task WithSharingTheContentScopeKeepsOnlyTheVersionRule()
    {
        var editable = ScopeRule.Where(ScopeFilter.Of(ScopeCondition.In("ContentItemId", ["a1"])));
        var scopes = new Scopes(editable);
        var provider = new ContentItemScopeProvider(null, null, scopes);

        var rule = await provider.BuildAsync(User("wes"), TestContext.Current.CancellationToken);

        Assert.Equal(AccessLevel.Editor, scopes.Asked);
        Assert.True(rule.Admits(column => column switch { "Published" => true, "Latest" => false, "ContentItemId" => "x", _ => null }));
        Assert.True(rule.Admits(column => column switch { "Published" => false, "Latest" => true, "ContentItemId" => "a1", _ => null }));
        Assert.False(rule.Admits(column => column switch { "Published" => false, "Latest" => true, "ContentItemId" => "x", _ => null }));

        // Editor nowhere: published versions only (which items is sharing's own provider's to say).
        var none = await new ContentItemScopeProvider(null, null, new Scopes(ScopeRule.None)).BuildAsync(User("wes"), TestContext.Current.CancellationToken);
        Assert.False(none.Admits(column => column switch { "Published" => false, "Latest" => true, _ => null }));
    }

    [Theory]
    [InlineData("ViewContent", true)]
    [InlineData("ViewOwnContent", true)]
    [InlineData("View_Article", true)]
    [InlineData("ViewOwn_Article", true)]
    [InlineData("EditContent", false)]
    [InlineData("View_", false)]
    [InlineData("ViewMediaContent", false)]
    public void TheDefaultViewGrantsAreTheContentViewPermissions(string permission, bool removed) =>
        Assert.Equal(removed, DefaultViewGrantsRemover.IsViewGrant(permission));
}
