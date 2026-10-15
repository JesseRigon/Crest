using Grove.Access;
using Grove.Access.Roles;
using Grove.Security;
using Grove.Security.Services;
using Grove.Access.Sharing;
using Grove.Access.Sharing.Services;
using Grove.Tests.Access.Conformance;
using System.Security.Claims;
using static Grove.Tests.Sharing.SharingWorld;

namespace Grove.Tests.Sharing;

/// <summary>
/// Phase 3 and the remaining § 1 and § 4 items of docs/sharing.md: business (derived) roles and
/// the role registry (39-48), system accounts (33-36), deleting and moving (69-70), comments
/// (71-72), items filed in a folder (74) and access indicators (98).
/// </summary>
public class SharingPhaseThreeTests
{
    private sealed class Accessor(CallerContext current) : ICallerContextAccessor
    {
        public CallerContext Current { get; set; } = current;
    }

    private sealed class Provider(params DerivedRoleGrant[] grants) : IDerivedRoleProvider
    {
        public string Name => "test";

        public IEnumerable<DerivedRoleDefinition> Roles => [new("Customer"), new("Vendor")];

        public Task<IReadOnlyList<DerivedRoleGrant>> GetRolesAsync(DerivedRoleContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DerivedRoleGrant>>(grants);
    }

    private sealed class DerivedAwareRoles(IRoleService inner, IEnumerable<IDerivedRoleProvider> providers) : IRoleService
    {
        public Task<IEnumerable<IRole>> GetRolesAsync() => inner.GetRolesAsync();

        public Task<IEnumerable<Claim>> GetRoleClaimsAsync(string role, CancellationToken cancellationToken = default) => inner.GetRoleClaimsAsync(role, cancellationToken);

        public Task<IEnumerable<string>> GetNormalizedRoleNamesAsync() => inner.GetNormalizedRoleNamesAsync();

        public ValueTask<bool> IsDerivedRoleAsync(string role) => ValueTask.FromResult(DerivedRoles.IsDerived(providers, role));
    }

    private sealed class StoredRoles(params string[] names) : IRoleService
    {
        public Task<IEnumerable<IRole>> GetRolesAsync() => Task.FromResult<IEnumerable<IRole>>([.. names.Select(name => new Role { RoleName = name })]);

        public Task<IEnumerable<Claim>> GetRoleClaimsAsync(string role, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Claim>>([]);

        public Task<IEnumerable<string>> GetNormalizedRoleNamesAsync() => Task.FromResult<IEnumerable<string>>([.. names.Select(name => name.ToUpperInvariant())]);
    }

    // ---- Business roles (39-48) -------------------------------------------------------------

    [Fact]
    public async Task ADerivedRoleOfAnOrganizationCountsOnlyWhileActingForIt()
    {
        var provider = new Provider(
            new DerivedRoleGrant("Customer", "Customer: Pat"),
            new DerivedRoleGrant("Vendor", "Vendor, member of Acme", "acme"));

        var acting = await DerivedRoles.GrantsAsync([provider], new DerivedRoleContext("pat", "acme"), TestContext.Current.CancellationToken);
        var elsewhere = await DerivedRoles.GrantsAsync([provider], new DerivedRoleContext("pat", "globex"), TestContext.Current.CancellationToken);
        var editor = await DerivedRoles.GrantsAsync([provider], new DerivedRoleContext("pat", null, AllOrganizations: true), TestContext.Current.CancellationToken);

        Assert.Equal(["Customer", "Vendor"], acting.Select(grant => grant.RoleName).Order());
        Assert.Equal(["Customer"], elsewhere.Select(grant => grant.RoleName));
        Assert.Equal(2, editor.Count);
    }

    [Fact]
    public async Task AProviderGrantsOnlyTheRolesItDeclares()
    {
        var provider = new Provider(new DerivedRoleGrant("Administrator", "smuggled"), new DerivedRoleGrant("Customer", "Customer: Pat"));

        var grants = await DerivedRoles.GrantsAsync([provider], new DerivedRoleContext("pat", null), TestContext.Current.CancellationToken);

        Assert.Equal(["Customer"], grants.Select(grant => grant.RoleName));
    }

    [Fact]
    public async Task DerivedRolesAreNeverAssignable()
    {
        var roles = new DerivedAwareRoles(new StoredRoles("Editor", "Customer"), [new Provider()]);

        var assignable = (await roles.GetAssignableRolesAsync()).Select(role => role.RoleName).ToList();

        Assert.Contains("Editor", assignable);
        Assert.DoesNotContain("Customer", assignable);
        Assert.True(await roles.IsDerivedRoleAsync("customer"));
        Assert.Equal("Customer", new DerivedRoleAssignmentException("Customer").RoleName);
    }

    [Fact]
    public async Task ABusinessRoleIsSharedWithLikeAnyRole()
    {
        // A derived role reaches sharing as a role on the caller: the example's Customer grant.
        var world = Example();
        var resolution = await world.Resolver().ResolveAsync(Member("pat", "acme", "Customer"), Ref("chair-original.jpg"), TestContext.Current.CancellationToken);

        Assert.Equal(AccessLevel.Viewer, resolution.Level);
    }

    // ---- System accounts (33-36) ------------------------------------------------------------

    [Fact]
    public void SystemAccountsAreIdsNoUserCanTake()
    {
        Assert.Equal("system:tenant", SystemAccounts.Tenant);
        Assert.Equal("acme", SystemAccounts.OrganizationOf(SystemAccounts.Organization("acme")));
        Assert.True(SystemAccounts.IsSystemAccount(SystemAccounts.Organization("acme")));
        Assert.False(SystemAccounts.IsSystemAccount("pat"));

        var tenantActor = new CallerContext { Tenant = "Default", Side = CallerSide.System, IsSystem = true, IsSuperUser = true };
        var organizationActor = new CallerContext { Tenant = "Default", Side = CallerSide.System, IsSystem = true, OrganizationId = "acme" };
        Assert.Equal(SystemAccounts.Tenant, tenantActor.ActorId);
        Assert.Equal(SystemAccounts.Organization("acme"), organizationActor.ActorId);
        Assert.Null(User("pat").SystemAccountId);
    }

    [Fact]
    public async Task AnOrganizationAccountOwnsItsDriveAndItsSystemActorAdministersIt()
    {
        var world = new SharingWorld();
        world.Files.Add("acme-drive", ownerAccount: SystemAccounts.Organization("acme"));
        world.Files.Add("contracts", "acme-drive");
        var actor = new CallerContext { Tenant = "Default", Side = CallerSide.System, IsSystem = true, OrganizationId = "acme" };
        var other = new CallerContext { Tenant = "Default", Side = CallerSide.System, IsSystem = true, OrganizationId = "globex" };

        var own = await world.Resolver().ResolveAsync(actor, Ref("contracts"), TestContext.Current.CancellationToken);
        var foreign = await world.Resolver().ResolveAsync(other, Ref("contracts"), TestContext.Current.CancellationToken);

        Assert.True(own.IsOwner);
        Assert.Equal(AccessLevel.Admin, own.Level);
        Assert.Equal(AccessLevel.None, foreign.Level);
    }

    [Fact]
    public async Task ASystemAccountIsNeverSharedWith()
    {
        var world = Example();
        var service = new SharingService(new Accessor(new CallerContext { Tenant = "Default", Side = CallerSide.Admin, UserId = "root", IsSuperUser = true }), new AllowAll(), world.Resolver(), world.Store, world.Kinds(), new AccessWorld.RecordingAuditor());

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetEntryAsync(Ref("images"), ShareEntry.Grant(SharePrincipal.User(SystemAccounts.Tenant), AccessLevel.Viewer), TestContext.Current.CancellationToken));
    }

    private sealed class AllowAll : IAccessDecision
    {
        public Task<AccessDecision> DecideAsync(CallerContext caller, string permission, object resource = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(AccessDecision.Allowed);
    }

    // ---- Deleting and moving (69-70) --------------------------------------------------------

    private static ShareChangeChecks Checks(SharingWorld world, CallerContext caller) => new(new Accessor(caller), world.Resolver(), world.Kinds());

    [Fact]
    public async Task DeletingAFolderNeedsManagerOnEverythingBelowWithoutNamingIt()
    {
        var world = Example();
        world.Share("images", ShareEntry.Grant(SharePrincipal.User("mo"), AccessLevel.Manager));
        world.Share("chair.psd", ShareEntry.Block(SharePrincipal.User("mo")));
        var checks = Checks(world, User("mo"));

        var refused = await Assert.ThrowsAsync<AccessDeniedException>(() => checks.RequireDeleteAsync(Ref("images"), TestContext.Current.CancellationToken));
        Assert.DoesNotContain("chair", refused.Message, StringComparison.OrdinalIgnoreCase);

        await checks.RequireDeleteAsync(Ref("chair-original.jpg"), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MovingAlsoNeedsEditorOnTheDestination()
    {
        var world = Example();
        world.Share("images", ShareEntry.Grant(SharePrincipal.User("mo"), AccessLevel.Manager));
        var checks = Checks(world, User("mo", "Staff"));

        // Staff are Viewers on Spec sheets: not enough to move into it.
        await Assert.ThrowsAsync<AccessDeniedException>(() => checks.RequireMoveAsync(Ref("chair-original.jpg"), Ref("spec-sheets"), TestContext.Current.CancellationToken));

        world.Share("spec-sheets", ShareEntry.Grant(SharePrincipal.User("mo"), AccessLevel.Editor));
        await Checks(world, User("mo", "Staff")).RequireMoveAsync(Ref("chair-original.jpg"), Ref("spec-sheets"), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WhatTheCallerCannotSeeIsNotFound()
    {
        var world = Example();
        var refused = await Assert.ThrowsAsync<AccessDeniedException>(() => Checks(world, User("cora", "Customer")).RequireDeleteAsync(Ref("chair.psd"), TestContext.Current.CancellationToken));

        Assert.Equal("Not found.", refused.Message);
    }

    // ---- Comments (71-72) -------------------------------------------------------------------

    private static ShareComments Comments(SharingWorld world, CallerContext caller) =>
        new(new Accessor(caller), world.Resolver(), world.Kinds(), world.Store, new AccessWorld.RecordingAuditor(), world.Clock);

    [Fact]
    public async Task ACommenterCommentsAndAViewerOnlyReads()
    {
        var world = Example();
        world.Share("images", ShareEntry.Grant(SharePrincipal.User("cam"), AccessLevel.Commenter));

        var comment = await Comments(world, User("cam")).AddAsync(Ref("chair-original.jpg"), "Nice chair", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("cam", comment.AuthorId);

        // Stored in the object's sharing metadata by default (requirement 72).
        Assert.Single(world.Store.Documents[Ref("chair-original.jpg")].Comments);

        var viewer = Comments(world, User("cora", "Customer"));
        Assert.Single(await viewer.ListAsync(Ref("chair-original.jpg"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AccessDeniedException>(() => viewer.AddAsync(Ref("chair-original.jpg"), "Me too", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OnlyTheAuthorOrAManagerRemovesAComment()
    {
        var world = Example();
        world.Share("images", ShareEntry.Grant(SharePrincipal.User("cam"), AccessLevel.Commenter), ShareEntry.Grant(SharePrincipal.User("cid"), AccessLevel.Commenter), ShareEntry.Grant(SharePrincipal.User("max"), AccessLevel.Manager));
        var comment = await Comments(world, User("cam")).AddAsync(Ref("chair-original.jpg"), "Draft", cancellationToken: TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<AccessDeniedException>(() => Comments(world, User("cid")).RemoveAsync(Ref("chair-original.jpg"), comment.Id, TestContext.Current.CancellationToken));
        await Comments(world, User("max")).RemoveAsync(Ref("chair-original.jpg"), comment.Id, TestContext.Current.CancellationToken);

        Assert.Empty(world.Store.Documents[Ref("chair-original.jpg")].Comments);
    }

    // ---- Items filed in a folder (74) -------------------------------------------------------

    private sealed class Filed(params (string Folder, ShareableRef Item)[] items) : IShareableChildSource
    {
        public Task<IReadOnlyList<ShareableRef>> ChildrenAsync(ShareableRef parent, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ShareableRef>>([.. items.Where(item => Ref(item.Folder) == parent).Select(item => item.Item)]);
    }

    [Fact]
    public async Task WhatIsFiledInAFolderIsBelowIt()
    {
        var world = Example();
        var filed = new ShareableRef("content", "page-1");
        var kinds = new ShareableKinds([world.Files], childSources: [new Filed(("spec-sheets", filed))]);

        var children = await kinds.ChildrenAsync(Ref("spec-sheets"), TestContext.Current.CancellationToken);

        Assert.Contains(Ref("spec.psd"), children);
        Assert.Contains(filed, children);
    }

    // ---- Access indicators (98) -------------------------------------------------------------

    [Fact]
    public async Task IndicatorsComeFromTheAccessIndex()
    {
        var world = Example();
        world.Files.Add("notes.txt", "images", owner: "olga");
        world.Share("images", ShareEntry.Block(SharePrincipal.Role("Customer")), ShareEntry.Block(SharePrincipal.Role("Staff")), ShareEntry.Block(SharePrincipal.Role("Marketing")));
        await world.IndexAllAsync();

        ShareIndicator Of(string id) => ShareIndicators.Of([world.AccessStore.Documents[Ref(id)]]);

        Assert.Equal(ShareIndicator.Public, Of("chair-final.jpg"));
        Assert.Equal(ShareIndicator.Shared, Of("spec-sheets"));
        Assert.Equal(ShareIndicator.Private, Of("notes.txt"));
    }
}
