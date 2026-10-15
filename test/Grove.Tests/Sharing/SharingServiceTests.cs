using Grove.Access;
using Grove.Access.Sharing;
using Grove.Access.Sharing.Services;
using Grove.Tests.Access.Conformance;
using static Grove.Tests.Sharing.SharingWorld;

namespace Grove.Tests.Sharing;

/// <summary>
/// Changing and reading sharing (requirements 66, 85, 99) and sharing's one registration with
/// the access decision (docs/sharing.md › Ruling): authoritative for registered kinds, silent
/// for everything else, never in the way of a tenant administrator.
/// </summary>
public class SharingServiceTests
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

    private static (SharingService Service, AccessWorld.RecordingAuditor Audit) Service(SharingWorld world, CallerContext caller, params string[] tenantPermissions)
    {
        var audit = new AccessWorld.RecordingAuditor();
        var service = new SharingService(new Accessor(caller), new Decision(tenantPermissions), world.Resolver(), world.Store, world.Kinds(), audit);
        return (service, audit);
    }

    private static CallerContext DriveAdmin(SharingWorld world)
    {
        world.Share("product-info", ShareEntry.Grant(SharePrincipal.User("da"), AccessLevel.Admin));
        return User("da");
    }

    [Fact]
    public async Task ADriveAdminSharesInsideTheirDriveAndTheChangeIsAuditedAndImmediate()
    {
        var world = Example();
        var (service, audit) = Service(world, DriveAdmin(world));

        await service.SetEntryAsync(Ref("images"), ShareEntry.Grant(SharePrincipal.User("dana"), AccessLevel.Viewer), TestContext.Current.CancellationToken);

        Assert.Equal(AccessLevel.Viewer, (await world.Resolver().ResolveAsync(User("dana"), Ref("chair-original.jpg"), TestContext.Current.CancellationToken)).Level);
        var recorded = Assert.Single(audit.Events);
        Assert.Equal("Sharing.SetEntry", recorded.Operation);
        Assert.Equal(Ref("images").ToString(), recorded.Resource);
    }

    [Fact]
    public async Task RemovingAccessTakesEffectOnTheNextDecision()
    {
        var world = Example();
        var (service, _) = Service(world, DriveAdmin(world));

        await service.RemoveEntryAsync(Ref("images"), SharePrincipal.Role("Marketing"), TestContext.Current.CancellationToken);

        Assert.Equal(AccessLevel.None, (await world.Resolver().ResolveAsync(User("mia", "Marketing"), Ref("chair-original.jpg"), TestContext.Current.CancellationToken)).Level);
    }

    [Fact]
    public async Task AnEditorCannotSeeOrChangeSharing()
    {
        var world = Example();
        var (service, _) = Service(world, User("mia", "Marketing"));

        await Assert.ThrowsAsync<AccessDeniedException>(() => service.SetEntryAsync(Ref("images"), ShareEntry.Grant(SharePrincipal.User("x"), AccessLevel.Viewer), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.GetEntriesAsync(Ref("images"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AccessDeniedException>(() => service.ExplainAsync(Ref("images"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ARefusalOnSomethingTheCallerCannotSeeSaysNotFound()
    {
        var world = Example();
        var (service, _) = Service(world, User("cora", "Customer"));

        var refused = await Assert.ThrowsAsync<AccessDeniedException>(() => service.GetEntriesAsync(Ref("chair.psd"), TestContext.Current.CancellationToken));

        Assert.Equal("Not found.", refused.Message);
    }

    [Fact]
    public async Task MakingSomethingPublicTakesShareWithAnyone()
    {
        var world = Example();
        var admin = DriveAdmin(world);

        var (without, _) = Service(world, admin);
        await Assert.ThrowsAsync<AccessDeniedException>(() => without.SetEntryAsync(Ref("images"), ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Viewer), TestContext.Current.CancellationToken));

        var (with, _) = Service(world, admin, SharingPermissionNames.ShareWithAnyone);
        await with.SetEntryAsync(Ref("images"), ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Viewer), TestContext.Current.CancellationToken);
        Assert.True((await world.Resolver().ResolveAsync(Visitor, Ref("chair-original.jpg"), TestContext.Current.CancellationToken)).IsPublic);
    }

    [Fact]
    public async Task AnyoneIsOnlyEverAViewer()
    {
        var world = Example();
        var (service, _) = Service(world, DriveAdmin(world), SharingPermissionNames.ShareWithAnyone);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetEntryAsync(Ref("images"), ShareEntry.Grant(SharePrincipal.Anyone, AccessLevel.Editor), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AdminIsGrantedOnlyOnARootAndNeverByARule()
    {
        var world = Example();
        var (service, _) = Service(world, DriveAdmin(world));

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetEntryAsync(Ref("images"), ShareEntry.Grant(SharePrincipal.User("x"), AccessLevel.Admin), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetRulesAsync(Ref("product-info"), [new ShareRule("r", "admins", [], [ShareEntry.Grant(SharePrincipal.User("x"), AccessLevel.Admin)])], TestContext.Current.CancellationToken));
        await service.SetEntryAsync(Ref("product-info"), ShareEntry.Grant(SharePrincipal.User("x"), AccessLevel.Admin), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TenantLevelSharingTakesItsOwnPermission()
    {
        var world = Example();
        var (without, _) = Service(world, DriveAdmin(world));
        await Assert.ThrowsAsync<AccessDeniedException>(() => without.SetEntryAsync(ShareableRef.TenantRoot, ShareEntry.Grant(SharePrincipal.Role("Auditor"), AccessLevel.Viewer), TestContext.Current.CancellationToken));

        var (with, _) = Service(world, User("t"), SharingPermissionNames.ManageTenantSharing);
        await with.SetEntryAsync(ShareableRef.TenantRoot, ShareEntry.Grant(SharePrincipal.Role("Auditor"), AccessLevel.Viewer), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ATenantAdministratorManagesAnySharing()
    {
        var world = Example();
        var admin = new CallerContext { Tenant = "Default", Side = CallerSide.Admin, UserId = "root", IsSuperUser = true };
        var (service, _) = Service(world, admin);

        await service.SetEntryAsync(Ref("chair.psd"), ShareEntry.Grant(SharePrincipal.User("x"), AccessLevel.Viewer), TestContext.Current.CancellationToken);
        Assert.NotEmpty(await service.ExplainAsync(Ref("chair.psd"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheExplanationListsEveryDecisionFromTheTenantDown()
    {
        var world = Example();
        var (service, _) = Service(world, DriveAdmin(world));

        var decisions = await service.ExplainAsync(Ref("chair.psd"), TestContext.Current.CancellationToken);

        Assert.Equal(decisions.OrderBy(d => d.Depth).Select(d => d.Depth), decisions.Select(d => d.Depth));
        Assert.Contains(decisions, d => d.RuleId == "rule-1" && d.Principal == SharePrincipal.Role("Customer") && d.Effect.IsBlock);
        Assert.DoesNotContain(decisions, d => d.RuleId == "rule-2");
    }

    [Fact]
    public async Task SharedWithMeListsExplicitSharesThatStillResolve()
    {
        var world = Example();
        world.Share("spec.psd", ShareEntry.Grant(SharePrincipal.User("dana"), AccessLevel.Commenter));
        world.Share("chair.psd", ShareEntry.Grant(SharePrincipal.User("sam"), AccessLevel.Viewer));
        var (forDana, _) = Service(world, User("dana"));
        var (forCustomer, _) = Service(world, User("cora", "Customer"));

        Assert.Equal([Ref("spec.psd")], await forDana.SharedWithMeAsync(TestContext.Current.CancellationToken));
        // The drive is shared with Customer, but chair.psd's rule blocks customers, and a tenant
        // grant is not "shared with me".
        Assert.Equal([Ref("product-info")], await forCustomer.SharedWithMeAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await Service(world, Visitor).Service.SharedWithMeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheMapperAnswersForARegisteredKindAndOnlyForThePermissionsItMaps()
    {
        var world = Example();
        var mapper = new SharingPermissionMapper([world.Files], world.Kinds(), world.Resolver());
        var image = world.Files.Nodes["chair.psd"];

        Assert.Equal([PermissionCandidate.Granted], await mapper.MapAsync("EditMedia", image, User("dana"), TestContext.Current.CancellationToken));
        Assert.Empty(await mapper.MapAsync("EditMedia", image, User("cora", "Customer"), TestContext.Current.CancellationToken));
        Assert.Null(await mapper.MapAsync("SomethingElse", image, User("dana"), TestContext.Current.CancellationToken));
        Assert.Null(await mapper.MapAsync("EditMedia", "not a file", User("dana"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ThroughTheDecisionSharingIsAuthoritativeWhateverTheRegistrationOrder()
    {
        // A module mapper registered first would grant everyone EditMedia on its files; the
        // sharing mapper is asked first and its denial stands.
        var world = Example();
        var permissive = new PermissiveMapper();
        var decision = new global::Grove.Access.Services.AccessDecisionService(
            new AccessWorld.PermissionRegistry(),
            [],
            [permissive, new SharingPermissionMapper([world.Files], world.Kinds(), world.Resolver())],
            new AccessWorld.RecordingAuditor());
        var customer = User("cora", "Customer");
        customer = new CallerContext { Tenant = customer.Tenant, Side = customer.Side, UserId = customer.UserId, Roles = customer.Roles, Permissions = new HashSet<string> { "EditMedia" } };

        var verdict = await decision.DecideAsync(customer, "EditMedia", world.Files.Nodes["chair.psd"], TestContext.Current.CancellationToken);

        Assert.False(verdict.IsAllowed);
        Assert.False(permissive.Asked);
    }

    [Fact]
    public async Task ATenantAdministratorPassesBeforeSharingIsAsked()
    {
        var world = Example();
        world.Files.Add("dead", inactive: true);
        var decision = new global::Grove.Access.Services.AccessDecisionService(
            new AccessWorld.PermissionRegistry(),
            [],
            [new SharingPermissionMapper([world.Files], world.Kinds(), world.Resolver())],
            new AccessWorld.RecordingAuditor());
        var admin = new CallerContext { Tenant = "Default", Side = CallerSide.Admin, UserId = "root", IsSuperUser = true };

        Assert.True((await decision.DecideAsync(admin, "DeleteMedia", world.Files.Nodes["dead"], TestContext.Current.CancellationToken)).IsAllowed);
    }

    [Fact]
    public async Task OfTwoKindsForOneNameTheHigherPriorityFeaturesKindAnswers()
    {
        // A third-party module ships its own "file" kind at a higher priority: it replaces
        // Grove's for every file (here it reports every file as owned by "x").
        var world = Example();
        var replacement = new ReplacementKind();
        var priority = new Priorities { [typeof(ReplacementKind)] = 10 };
        var kinds = new ShareableKinds([world.Files, replacement], priority);
        var resolver = new SharingResolver(kinds, world.Store, world.Clock);

        Assert.Same(replacement, kinds.Find(Kind));
        Assert.Equal(AccessLevel.Admin, (await resolver.ResolveAsync(User("x"), Ref("chair.psd"), TestContext.Current.CancellationToken)).Level);
    }

    [Fact]
    public async Task TwoKindsForOneNameAtTheSamePriorityAreAConflictAndAllowNothing()
    {
        var world = Example();
        var kinds = new ShareableKinds([world.Files, new ReplacementKind()]);
        var mapper = new SharingPermissionMapper([world.Files, new ReplacementKind()], kinds, new SharingResolver(kinds, world.Store, world.Clock));

        Assert.Null(kinds.Find(Kind));
        // Claimed and denied, never left to a module mapper to grant.
        Assert.Empty(await mapper.MapAsync("ViewMedia", world.Files.Nodes["chair-original.jpg"], User("cora", "Customer"), TestContext.Current.CancellationToken));
    }

    internal sealed class Priorities : Dictionary<Type, int>, global::Grove.Environment.Extensions.Features.IProviderPriority
    {
        public int PriorityOf(Type providerType) => TryGetValue(providerType, out var value) ? value : 0;
    }

    private sealed class ReplacementKind : IShareableKind
    {
        public string Name => Kind;

        public bool TryIdentify(object resource, out string id)
        {
            id = (resource as SharingWorld.TestKind.Node)?.Id;
            return id is not null;
        }

        public Task<ShareableObject> GetAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ShareableObject { Ref = new ShareableRef(Kind, id), OwnerUserId = "x" });

        public AccessLevel? LevelFor(string permission) => AccessLevel.Viewer;
    }

    private sealed class PermissiveMapper : IResourcePermissionMapper
    {
        public bool Asked { get; private set; }

        public ValueTask<IReadOnlyList<PermissionCandidate>> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken = default)
        {
            Asked = true;
            return ValueTask.FromResult<IReadOnlyList<PermissionCandidate>>([PermissionCandidate.Granted]);
        }
    }
}
