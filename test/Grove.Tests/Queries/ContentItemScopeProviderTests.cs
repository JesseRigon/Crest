#nullable enable
using Grove.Access;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Security;

namespace Grove.Tests.Queries;

public class ContentItemScopeProviderTests
{
    private static readonly CallerContext s_user = new() { Tenant = "test", Side = CallerSide.Site, UserId = "u1" };

    private static async Task<ScopeRule> BuildAsync(CallerContext caller, params (string Permission, string Type, bool AsOwner)[] grants)
    {
        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.ListTypeDefinitionsAsync()).ReturnsAsync(
        [
            new ContentTypeDefinition("Blog", "Blog"),
            new ContentTypeDefinition("Page", "Page"),
            new ContentTypeDefinition("Note", "Note"),
        ]);

        return await new ContentItemScopeProvider(definitions.Object, new GrantedDecision(grants)).BuildAsync(caller);
    }

    private static bool Admits(ScopeRule rule, string type, bool published, bool latest, string owner = "u1")
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ContentType"] = type,
            ["Published"] = published,
            ["Latest"] = latest,
            ["Owner"] = owner,
        };

        return rule.Admits(column => row.TryGetValue(column, out var value) ? value : null);
    }

    [Fact]
    public async Task ViewOnly_SeesPublishedVersionsOnly()
    {
        var rule = await BuildAsync(s_user, ("ViewContent", "Blog", false));

        Assert.True(Admits(rule, "Blog", published: true, latest: true));
        Assert.True(Admits(rule, "Blog", published: true, latest: false, owner: "u2"));
        Assert.False(Admits(rule, "Blog", published: false, latest: true));
        Assert.False(Admits(rule, "Blog", published: false, latest: false));
        Assert.False(Admits(rule, "Page", published: true, latest: true));
    }

    [Theory]
    [InlineData("EditContent")]
    [InlineData("PreviewContent")]
    public async Task EditorOrPreviewer_SeesPublishedAndLatest(string permission)
    {
        var rule = await BuildAsync(s_user, (permission, "Page", false));

        Assert.True(Admits(rule, "Page", published: false, latest: true, owner: "u2"));
        Assert.True(Admits(rule, "Page", published: true, latest: false, owner: "u2"));
        Assert.False(Admits(rule, "Page", published: false, latest: false, owner: "u2"));
    }

    [Fact]
    public async Task OwnEditor_SeesOwnDraftsAndNothingOfOthers()
    {
        var rule = await BuildAsync(s_user, ("EditContent", "Note", true));

        Assert.True(Admits(rule, "Note", published: false, latest: true, owner: "u1"));
        Assert.False(Admits(rule, "Note", published: false, latest: true, owner: "u2"));
        Assert.False(Admits(rule, "Note", published: true, latest: true, owner: "u2"));
        Assert.False(Admits(rule, "Note", published: false, latest: false, owner: "u1"));
    }

    [Fact]
    public async Task Viewer_WhoEditsOwn_SeesOthersPublishedAndOwnDrafts()
    {
        var rule = await BuildAsync(s_user, ("ViewContent", "Blog", false), ("EditContent", "Blog", true));

        Assert.True(Admits(rule, "Blog", published: true, latest: false, owner: "u2"));
        Assert.False(Admits(rule, "Blog", published: false, latest: true, owner: "u2"));
        Assert.True(Admits(rule, "Blog", published: false, latest: true, owner: "u1"));
    }

    [Fact]
    public async Task OwnViewer_SeesOwnPublishedOnly()
    {
        var rule = await BuildAsync(s_user, ("ViewContent", "Blog", true));

        Assert.True(Admits(rule, "Blog", published: true, latest: true, owner: "u1"));
        Assert.False(Admits(rule, "Blog", published: false, latest: true, owner: "u1"));
        Assert.False(Admits(rule, "Blog", published: true, latest: true, owner: "u2"));
    }

    [Fact]
    public async Task NoGrant_IsNone()
    {
        var rule = await BuildAsync(s_user);

        Assert.Equal(ScopeKind.None, rule.Kind);
    }

    [Fact]
    public async Task AnonymousCaller_GetsNoOwnVariant()
    {
        var rule = await BuildAsync(CallerContext.Anonymous("test", CallerSide.Site), ("EditContent", "Note", true));

        Assert.Equal(ScopeKind.None, rule.Kind);
    }

    [Fact]
    public void Version_IsBumped()
    {
        Assert.Equal(3, new ContentItemScopeProvider(Mock.Of<IContentDefinitionManager>(), new GrantedDecision([])).Version);
    }

    private sealed class GrantedDecision((string Permission, string Type, bool AsOwner)[] grants) : IAccessDecision
    {
        public Task<AccessDecision> DecideAsync(CallerContext caller, string permission, object? resource = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(resource is ContentTypeProbe probe && grants.Contains((permission, probe.ContentType, probe.AsOwner))
                ? AccessDecision.Allowed
                : AccessDecision.Denied("not granted"));
    }
}
