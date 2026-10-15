using Grove.Access;
using Grove.Sharing;
using Grove.Sharing.Recipes;
using Grove.Sharing.Services;
using Grove.Tests.Access.Conformance;
using static Grove.Tests.Sharing.SharingWorld;

namespace Grove.Tests.Sharing;

/// <summary>
/// Rebuilding the access index from the kinds' roots, Admin assignment by a tenant permission
/// (media requirement 31), organization-scoped role principals, the recipe step's principals,
/// and the mapper choosing among kinds that recognise the same resource by the permission.
/// </summary>
public class SharingPhaseTwoExtensionsTests
{
    private sealed class Accessor(CallerContext current) : ICallerContextAccessor
    {
        public CallerContext Current { get; set; } = current;
    }

    private sealed class Decision(params string[] granted) : IAccessDecision
    {
        public Task<AccessDecision> DecideAsync(CallerContext caller, string permission, object resource = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(caller.IsSuperUser || granted.Contains(permission) ? AccessDecision.Allowed : AccessDecision.Denied($"'{permission}' is not granted."));
    }

    private static (SharingService Service, AccessWorld.RecordingAuditor Audit) Service(SharingWorld world, CallerContext caller, params string[] permissions)
    {
        var audit = new AccessWorld.RecordingAuditor();
        return (new SharingService(new Accessor(caller), new Decision(permissions), world.Resolver(), world.Store, world.Kinds(), audit, world.Indexer(), world.AccessStore, world.Clock), audit);
    }

    [Fact]
    public async Task ARebuildIndexesEverythingBelowTheRootsInTheBackground()
    {
        var world = Example();
        var indexer = world.Indexer();

        await indexer.QueueRebuildAsync(TestContext.Current.CancellationToken);
        Assert.Empty(world.AccessStore.Documents);

        // The first pass turns the rebuild into one queued subtree per root; the next runs them.
        await indexer.RecalculateQueuedAsync(10, TestContext.Current.CancellationToken);
        Assert.Equal(Ref("product-info"), Assert.Single(world.AccessStore.Queue).Ref);
        await indexer.RecalculateQueuedAsync(10, TestContext.Current.CancellationToken);

        Assert.Empty(world.AccessStore.Queue);
        Assert.Equal(world.Files.Nodes.Count, world.AccessStore.Documents.Count);
        foreach (var id in world.Files.Nodes.Keys)
        {
            Assert.Empty(await indexer.VerifyAsync(Ref(id), TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task AHolderOfTheAssignmentPermissionAddsAndRemovesAdminsOnARootOnly()
    {
        var world = Example();
        var (service, audit) = Service(world, User("hr"), TestKind.ManageDrives);

        await service.SetEntryAsync(Ref("product-info"), ShareEntry.Grant(SharePrincipal.User("ada"), AccessLevel.Admin), TestContext.Current.CancellationToken);
        Assert.Equal(AccessLevel.Admin, (await world.Resolver().ResolveAsync(User("ada"), Ref("chair.psd"), TestContext.Current.CancellationToken)).Level);
        Assert.EndsWith($"(through {TestKind.ManageDrives})", Assert.Single(audit.Events).Reason);

        // They see the Admin grants they manage, nothing else of the drive's sharing.
        Assert.All(await service.GetEntriesAsync(Ref("product-info"), TestContext.Current.CancellationToken), entry => Assert.Equal(AccessLevel.Admin, entry.Level));

        // Not another kind of entry, not below the root.
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.SetEntryAsync(Ref("product-info"), ShareEntry.Grant(SharePrincipal.User("ada"), AccessLevel.Viewer), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.SetEntryAsync(Ref("images"), ShareEntry.Grant(SharePrincipal.User("ada"), AccessLevel.Admin), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.RemoveEntryAsync(Ref("product-info"), SharePrincipal.Role("Customer"), TestContext.Current.CancellationToken));

        await service.RemoveEntryAsync(Ref("product-info"), SharePrincipal.User("ada"), TestContext.Current.CancellationToken);
        Assert.Equal(AccessLevel.None, (await world.Resolver().ResolveAsync(User("ada"), Ref("chair.psd"), TestContext.Current.CancellationToken)).Level);
        Assert.Equal(2, audit.Events.Count);
    }

    [Fact]
    public async Task WithoutThePermissionAdminsAreTheTreesOwnBusiness()
    {
        var world = Example();
        var (service, _) = Service(world, User("hr"));

        await Assert.ThrowsAsync<AccessDeniedException>(() => service.SetEntryAsync(Ref("product-info"), ShareEntry.Grant(SharePrincipal.User("ada"), AccessLevel.Admin), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnOrganizationScopedRoleReachesOnlyItsOrganizationsMembers()
    {
        var world = Example();
        world.Share("spec-sheets", ShareEntry.Grant(SharePrincipal.Role("MemberAdmin", "acme"), AccessLevel.Editor));
        var resolver = world.Resolver();

        Assert.Equal(AccessLevel.Editor, (await resolver.ResolveAsync(Member("al", "acme", "MemberAdmin"), Ref("spec.psd"), TestContext.Current.CancellationToken)).Level);
        // Elsewhere the role's Editor does not reach: another organization's member and a staff
        // user with the same role name keep only Spec sheets' tenant grant (requirement 37).
        Assert.Equal(AccessLevel.Viewer, (await resolver.ResolveAsync(Member("bo", "beta", "MemberAdmin"), Ref("spec.psd"), TestContext.Current.CancellationToken)).Level);
        Assert.Equal(AccessLevel.Viewer, (await resolver.ResolveAsync(User("cy", "MemberAdmin"), Ref("spec.psd"), TestContext.Current.CancellationToken)).Level);

        // Its index row carries the organization.
        await world.IndexAllAsync();
        var row = Assert.Single((await world.AccessStore.GetAsync(Ref("spec.psd")))!.Rows, row => row.OrganizationId == "acme");
        Assert.Equal(SharePrincipal.Role("MemberAdmin", "acme"), row.Principal);
    }

    [Fact]
    public async Task OnlyARoleCanBeLimitedToAnOrganization()
    {
        var world = Example();
        var (service, _) = Service(world, new CallerContext { Tenant = "Default", Side = CallerSide.Admin, UserId = "root", IsSuperUser = true });

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetEntryAsync(Ref("images"), ShareEntry.Grant(new SharePrincipal(SharePrincipalKind.User, "u", "acme"), AccessLevel.Viewer), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("anyone", SharePrincipalKind.Anyone, null, null)]
    [InlineData("tenant", SharePrincipalKind.Tenant, null, null)]
    [InlineData("role:Customer", SharePrincipalKind.Role, "Customer", null)]
    [InlineData("role:MemberAdmin@acme", SharePrincipalKind.Role, "MemberAdmin", "acme")]
    [InlineData("user:sam", SharePrincipalKind.User, "sam", null)]
    [InlineData("organization:acme", SharePrincipalKind.Organization, "acme", null)]
    public void TheRecipeStepParsesPrincipals(string text, SharePrincipalKind kind, string id, string organization)
    {
        Assert.True(SharingStep.TryParsePrincipal(text, out var principal));
        Assert.Equal(new SharePrincipal(kind, id, organization), principal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("user")]
    [InlineData("anyone:x")]
    [InlineData("group:x")]
    public void TheRecipeStepRefusesWhatIsNotAPrincipal(string text) =>
        Assert.False(SharingStep.TryParsePrincipal(text, out _));

    /// <summary>A kind that claims every string, as Media's path kind does.</summary>
    private sealed class StringKind(string name, string permission) : IShareableKind
    {
        public string Name => name;

        public bool TryIdentify(object resource, out string id)
        {
            id = resource as string;
            return id is not null;
        }

        public Task<ShareableObject> GetAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ShareableObject { Ref = new ShareableRef(name, id) });

        public AccessLevel? LevelFor(string asked) => asked == permission ? AccessLevel.Viewer : null;
    }

    [Fact]
    public void TheMapperPicksTheKindThatMapsThePermission()
    {
        IShareableKind[] registered = [new StringKind("media", "ViewMedia"), new StringKind("contenttype", "ViewContent")];
        var kinds = new ShareableKinds(registered);

        Assert.True(kinds.TryIdentify(registered, "Article", "ViewContent", out var target, out _));
        Assert.Equal(new ShareableRef("contenttype", "Article"), target);
        Assert.True(kinds.TryIdentify(registered, "a/b.jpg", "ViewMedia", out target, out _));
        Assert.Equal("media", target.Kind);
        Assert.False(kinds.TryIdentify(registered, "x", "ManageSettings", out _, out _));
    }
}
