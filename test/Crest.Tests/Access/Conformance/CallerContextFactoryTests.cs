using System.Security.Claims;
using Crest.Access;
using Crest.Access.Services;
using Crest.Environment.Shell;
using Crest.Roles;
using Crest.Security;
using Crest.Security.Permissions;
using Crest.Settings;
using Crest.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace Crest.Tests.Access.Conformance;

/// <summary>
/// The caller is built from the identity plus server-held state, once per request, and a
/// rights write reaches the next request (docs/access.md › One calculation per request,
/// Invalidation). The roles, users and site here are in-memory; the role grants can change
/// between two builds the way an admin edits a role between two requests.
/// </summary>
public class CallerContextFactoryTests
{
    private sealed class TestUser : IUser
    {
        public string UserName { get; init; }

        public string[] RoleNames { get; set; } = [];
    }

    private sealed class Version : IPermissionVersion
    {
        public long Value { get; set; } = 1;

        public Task<long> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);

        public Task BumpAsync(CancellationToken cancellationToken = default)
        {
            Value++;
            return Task.CompletedTask;
        }
    }

    private sealed class World
    {
        public Dictionary<string, IUser> Users { get; } = new(StringComparer.Ordinal)
        {
            ["u-editor"] = new TestUser { UserName = "editor", RoleNames = ["Editor"] },
            ["u-admin"] = new TestUser { UserName = "admin", RoleNames = ["Administrator"] },
            ["u-owner"] = new TestUser { UserName = "owner" },
        };

        public Dictionary<string, List<string>> RoleGrants { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            [PlatformConstants.Roles.Anonymous] = ["ViewContent_Page"],
            [PlatformConstants.Roles.Authenticated] = ["ViewOwnProfile"],
            ["Editor"] = ["EditContent", "PublishContent"],
            ["Administrator"] = [],
            ["Importer"] = ["ImportRemoteInstances"],
        };

        public string SuperUser { get; set; } = "u-owner";

        public Version Version { get; } = new();

        public List<ICallerContextContributor> Contributors { get; } = [];

        public List<ICallerRequestResolver> Resolvers { get; } = [];

        public int RoleReads { get; set; }

        public IMemoryCache Cache { get; } = new MemoryCache(new MemoryCacheOptions());

        public CallerContextFactory Factory()
        {
            var users = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
            users.Setup(m => m.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((string id) => Users.TryGetValue(id, out var user) ? user : null);
            users.Setup(m => m.GetRolesAsync(It.IsAny<IUser>())).ReturnsAsync((IUser user) => user is TestUser test ? test.RoleNames.ToList() : ((global::Crest.Users.Models.User)user).RoleNames.ToList());

            var roles = new Mock<RoleManager<IRole>>(Mock.Of<IRoleStore<IRole>>(), Array.Empty<IRoleValidator<IRole>>(), null, null, null);
            roles.Setup(m => m.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((string name) =>
            {
                RoleReads++;
                return RoleGrants.ContainsKey(name) ? new Role { RoleName = name } : null;
            });
            roles.Setup(m => m.GetClaimsAsync(It.IsAny<IRole>())).ReturnsAsync((IRole role) =>
                RoleGrants[role.RoleName].Select(permission => new Claim(Permission.ClaimType, permission)).ToList());

            var systemRoles = new Mock<ISystemRoleProvider>();
            systemRoles.Setup(m => m.GetAdminRole()).Returns(new Role { RoleName = "Administrator" });

            var site = new Mock<ISite>();
            site.SetupGet(m => m.SuperUser).Returns(() => SuperUser);
            var siteService = new Mock<ISiteService>();
            siteService.Setup(m => m.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

            return new CallerContextFactory(
                new ShellSettings { Name = AccessWorld.Tenant },
                users.Object,
                roles.Object,
                systemRoles.Object,
                siteService.Object,
                Version,
                new CallerStateCache(Cache),
                Resolvers,
                Contributors,
                access: Access);
        }

        public IUserAccessReader Access { get; set; }
    }

    private static ClaimsPrincipal Signed(string userId, params Claim[] extra) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId), .. extra], "Identity.Application"));

    /// <summary>A token-authenticated principal (OpenIddict validation, a remote deployment key).</summary>
    private static ClaimsPrincipal Bearer(string clientId, params Claim[] extra) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, clientId), .. extra], "OpenIddict.Validation.AspNetCore"));

    private static CallerRequest Request(ClaimsPrincipal principal, CallerSide side = CallerSide.Admin, string organization = null, string impersonator = null) =>
        new(principal, side, organization, "en", impersonator);

    [Fact]
    public async Task AnAnonymousCallerHoldsTheAnonymousRoleOnly()
    {
        var caller = await new World().Factory().CreateAsync(Request(null, CallerSide.Site), TestContext.Current.CancellationToken);

        Assert.False(caller.IsAuthenticated);
        Assert.False(caller.IsSuperUser);
        Assert.Equal(new[] { "ViewContent_Page" }, caller.Permissions);
        Assert.Empty(caller.Roles);
    }

    [Fact]
    public async Task ASignedInUserAddsTheAuthenticatedRoleAndTheirOwn()
    {
        var caller = await new World().Factory().CreateAsync(Request(Signed("u-editor")), TestContext.Current.CancellationToken);

        Assert.Equal("u-editor", caller.UserId);
        Assert.Equal("editor", caller.UserName);
        Assert.Equal(new[] { "Editor" }, caller.Roles);
        Assert.Superset(new HashSet<string> { "ViewContent_Page", "ViewOwnProfile", "EditContent", "PublishContent" }, caller.Permissions.ToHashSet());
        Assert.Equal(4, caller.Permissions.Count);
    }

    [Fact]
    public async Task RightsComeFromTheRolesNeverFromClaimsOnThePrincipal()
    {
        // A Permission claim smuggled into the cookie or a token grants nothing.
        var principal = Signed("u-editor", new Claim(Permission.ClaimType, "ManageUsers"), new Claim(ClaimTypes.Role, "Administrator"));

        var caller = await new World().Factory().CreateAsync(Request(principal), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("ManageUsers", caller.Permissions);
        Assert.DoesNotContain("Administrator", caller.Roles);
        Assert.False(caller.IsSuperUser);
    }

    [Fact]
    public async Task TheAdminRoleAndTheSiteSuperUserAreSuperUsers()
    {
        var world = new World();
        var factory = world.Factory();

        Assert.True((await factory.CreateAsync(Request(Signed("u-admin")), TestContext.Current.CancellationToken)).IsSuperUser);
        Assert.True((await factory.CreateAsync(Request(Signed("u-owner")), TestContext.Current.CancellationToken)).IsSuperUser);
        Assert.False((await factory.CreateAsync(Request(Signed("u-editor")), TestContext.Current.CancellationToken)).IsSuperUser);
    }

    private sealed class Projection(UserAccess access) : IUserAccessReader
    {
        public List<AccessReach> Reads { get; } = [];

        public Task<UserAccess> ReadAsync(string userId, AccessReach reach, CancellationToken cancellationToken = default)
        {
            Reads.Add(reach);
            return Task.FromResult(userId == access.UserId && reach == access.Reach ? access : null);
        }
    }

    [Fact]
    public async Task ASignedInUserHoldsTheTenantProjectionNotTheRolesOnTheirRecord()
    {
        // docs/permissions.md › Projection: the caller is built from the user's tenant rows
        // (groups, roles, sets, overrides resolved), plus the baseline every caller has.
        var world = new World();
        var projection = new Projection(new UserAccess(
            "u-editor",
            AccessReach.Tenant,
            new HashSet<string>(["Support desk"]),
            new HashSet<string>(["Support"], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(),
            [new UserAccessGrant("ManageOrgMembers", "role:Support")],
            1));
        world.Access = projection;

        var caller = await world.Factory().CreateAsync(Request(Signed("u-editor")), TestContext.Current.CancellationToken);

        Assert.Equal([AccessReach.Tenant], projection.Reads);
        Assert.Contains("Support", caller.Roles);
        Assert.DoesNotContain("Editor", caller.Roles);
        Assert.Contains("ManageOrgMembers", caller.Permissions);
        Assert.DoesNotContain("PublishContent", caller.Permissions);
        Assert.Contains("ViewOwnProfile", caller.Permissions);
    }

    [Fact]
    public async Task ARightRevokedBetweenTwoRequestsIsGoneOnTheSecond()
    {
        var world = new World();
        var factory = world.Factory();
        var first = await factory.CreateAsync(Request(Signed("u-editor")), TestContext.Current.CancellationToken);

        // An admin removes PublishContent from the Editor role; the role write bumps the version.
        world.RoleGrants["Editor"].Remove("PublishContent");
        await new PermissionVersionBumper(world.Version).RoleUpdatedAsync("Editor");

        var second = await factory.CreateAsync(Request(Signed("u-editor")), TestContext.Current.CancellationToken);

        Assert.Contains("PublishContent", first.Permissions);
        Assert.DoesNotContain("PublishContent", second.Permissions);
        Assert.True(second.PermissionVersion > first.PermissionVersion);
        Assert.NotEqual(first.ScopeSignature, second.ScopeSignature);
    }

    [Fact]
    public async Task ARoleRemovedFromAUserIsGoneOnTheNextRequest()
    {
        var world = new World();
        var factory = world.Factory();
        await factory.CreateAsync(Request(Signed("u-editor")), TestContext.Current.CancellationToken);

        // The user store bumps the version when it writes a role membership; an ordinary update
        // (a profile, a lockout counter) does not.
        var before = await world.Version.GetAsync(TestContext.Current.CancellationToken);
        await new PermissionVersionBumper(world.Version).UpdatedAsync(null);
        Assert.Equal(before, await world.Version.GetAsync(TestContext.Current.CancellationToken));

        ((TestUser)world.Users["u-editor"]).RoleNames = [];
        await world.Version.BumpAsync(TestContext.Current.CancellationToken);

        var caller = await factory.CreateAsync(Request(Signed("u-editor")), TestContext.Current.CancellationToken);

        Assert.Empty(caller.Roles);
        Assert.DoesNotContain("EditContent", caller.Permissions);
    }

    [Fact]
    public async Task ASuperUserHandedOverIsNoLongerSuperOnTheNextRequest()
    {
        var world = new World();
        var factory = world.Factory();
        Assert.True((await factory.CreateAsync(Request(Signed("u-owner")), TestContext.Current.CancellationToken)).IsSuperUser);

        // The settings step bumps the version when it writes the super user.
        world.SuperUser = "u-editor";
        await world.Version.BumpAsync(TestContext.Current.CancellationToken);

        Assert.False((await factory.CreateAsync(Request(Signed("u-owner")), TestContext.Current.CancellationToken)).IsSuperUser);
        Assert.True((await factory.CreateAsync(Request(Signed("u-editor")), TestContext.Current.CancellationToken)).IsSuperUser);
    }

    [Fact]
    public async Task UnderTheSameVersionTheStateIsBuiltOnce()
    {
        var world = new World();
        var factory = world.Factory();

        await factory.CreateAsync(Request(Signed("u-editor")), TestContext.Current.CancellationToken);
        var reads = world.RoleReads;
        await factory.CreateAsync(Request(Signed("u-editor")), TestContext.Current.CancellationToken);

        Assert.Equal(reads, world.RoleReads);
    }

    [Fact]
    public async Task EachSideIsItsOwnState()
    {
        var world = new World();
        world.Contributors.Add(new SideContributor());
        var factory = world.Factory();

        var admin = await factory.CreateAsync(Request(Signed("u-editor"), CallerSide.Admin), TestContext.Current.CancellationToken);
        var site = await factory.CreateAsync(Request(Signed("u-editor"), CallerSide.Site), TestContext.Current.CancellationToken);

        Assert.Contains("AdminOnly", admin.Permissions);
        Assert.DoesNotContain("AdminOnly", site.Permissions);
        Assert.NotEqual(admin.ScopeSignature, site.ScopeSignature);
    }

    [Fact]
    public async Task TwoUsersWithTheSameRolesNeverShareAScopeSignature()
    {
        var world = new World();
        world.Users["u-editor-2"] = new TestUser { UserName = "editor2", RoleNames = ["Editor"] };
        var factory = world.Factory();

        var one = await factory.CreateAsync(Request(Signed("u-editor")), TestContext.Current.CancellationToken);
        var two = await factory.CreateAsync(Request(Signed("u-editor-2")), TestContext.Current.CancellationToken);

        Assert.Equal(one.Permissions.Order(), two.Permissions.Order());
        Assert.NotEqual(one.ScopeSignature, two.ScopeSignature);
    }

    [Fact]
    public async Task AnImpersonatorIsARequestFactNeverCached()
    {
        var world = new World();
        var factory = world.Factory();

        var impersonated = await factory.CreateAsync(Request(Signed("u-editor"), impersonator: "u-admin"), TestContext.Current.CancellationToken);
        var plain = await factory.CreateAsync(Request(Signed("u-editor")), TestContext.Current.CancellationToken);

        Assert.Equal("u-admin", impersonated.ImpersonatorUserId);
        Assert.Null(plain.ImpersonatorUserId);
    }

    [Fact]
    public void AnAnonymousCallersCacheKeyIgnoresTheRequestedOrganization()
    {
        // An unauthenticated client cannot grow the cache by varying X-Org.
        Assert.Equal(
            CallerStateCache.Key("t", null, CallerSide.Member, "org-1"),
            CallerStateCache.Key("t", null, CallerSide.Member, "org-2"));
        Assert.NotEqual(
            CallerStateCache.Key("t", "u", CallerSide.Member, "org-1"),
            CallerStateCache.Key("t", "u", CallerSide.Member, "org-2"));
    }

    [Fact]
    public async Task APrincipalWithNoUserRecordIsAnApplicationWithItsRolesRights()
    {
        var principal = Bearer("app-1", new Claim(ClaimTypes.Role, "Importer"));

        var caller = await new World().Factory().CreateAsync(Request(principal, CallerSide.Site), TestContext.Current.CancellationToken);

        Assert.Equal(CallerClasses.Application, caller.UserClass);
        Assert.Contains("ImportRemoteInstances", caller.Permissions);
        Assert.DoesNotContain("EditContent", caller.Permissions);
        Assert.False(caller.IsSuperUser);
    }

    [Fact]
    public async Task AnApplicationGrantedTheAdminRoleByItsIssuerIsASuperUser()
    {
        // A token's roles are the application's, set by whoever registered it; the factory
        // reads them like a user's.
        var principal = Bearer("app-2", new Claim(ClaimTypes.Role, "Administrator"));

        var caller = await new World().Factory().CreateAsync(Request(principal, CallerSide.Site), TestContext.Current.CancellationToken);

        Assert.Equal(CallerClasses.Application, caller.UserClass);
        Assert.True(caller.IsSuperUser);
    }

    [Fact]
    public async Task ADisabledUserKeepsNoRightsAndIsNoApplication()
    {
        // Disabled while signed in, or the actor of a running workflow: the account's roles
        // stop counting at once (the disable bumps the version).
        var world = new World();
        world.Users["u-off"] = new global::Crest.Users.Models.User { UserId = "u-off", UserName = "off", RoleNames = ["Editor", "Administrator"], IsEnabled = false };

        var caller = await world.Factory().CreateAsync(Request(Signed("u-off")), TestContext.Current.CancellationToken);

        Assert.Empty(caller.Roles);
        Assert.False(caller.IsSuperUser);
        Assert.Null(caller.UserClass);
        Assert.DoesNotContain("EditContent", caller.Permissions);
    }

    [Fact]
    public async Task AWorkflowActorForADeletedUserIsNotAnApplication()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u-gone"), new Claim(ClaimTypes.Role, "Administrator")], "Identity.WorkflowActor"));

        var caller = await new World().Factory().CreateAsync(Request(principal), TestContext.Current.CancellationToken);

        Assert.Null(caller.UserClass);
        Assert.Empty(caller.Roles);
        Assert.False(caller.IsSuperUser);
    }

    [Fact]
    public async Task ACookieForADeletedUserIsNotAnApplication()
    {
        // The identity cookie carries role claims for display; a user deleted since sign-in
        // keeps the cookie until the stamp check. It must not become an application caller
        // holding the roles the cookie lists.
        var principal = Signed("u-deleted", new Claim(ClaimTypes.Role, "Administrator"), new Claim(ClaimTypes.Role, "Editor"));

        var caller = await new World().Factory().CreateAsync(Request(principal), TestContext.Current.CancellationToken);

        Assert.Null(caller.UserClass);
        Assert.Empty(caller.Roles);
        Assert.False(caller.IsSuperUser);
        Assert.DoesNotContain("EditContent", caller.Permissions);
    }

    [Fact]
    public async Task AContributorsDenialRefusesTheRequest()
    {
        var world = new World();
        world.Contributors.Add(new DenyingContributor());

        var denied = await Assert.ThrowsAsync<CallerDeniedException>(() =>
            world.Factory().CreateAsync(Request(Signed("u-editor"), CallerSide.Member, "org-x"), TestContext.Current.CancellationToken));

        Assert.Contains("org-x", denied.Message);
    }

    [Fact]
    public async Task ARequestResolverRunsBeforeTheCache()
    {
        var world = new World();
        world.Resolvers.Add(new SessionOrganization("org-session"));
        world.Contributors.Add(new OrganizationContributor());
        var factory = world.Factory();

        var caller = await factory.CreateAsync(Request(Signed("u-editor"), CallerSide.Member), TestContext.Current.CancellationToken);

        Assert.Equal("org-session", caller.OrganizationId);
    }

    [Fact]
    public async Task TheSystemActorIsATenantAdminOnTheSystemSide()
    {
        var caller = await new World().Factory().CreateSystemAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(caller.IsSystem);
        Assert.True(caller.IsSuperUser);
        Assert.Equal(CallerSide.System, caller.Side);
        Assert.Null(caller.UserId);
    }

    [Fact]
    public async Task AnOrganizationSystemActorIsNotATenantSuperUser()
    {
        var world = new World();
        world.Contributors.Add(new OrganizationContributor());

        var caller = await world.Factory().CreateSystemAsync("org-1", TestContext.Current.CancellationToken);

        Assert.True(caller.IsSystem);
        Assert.False(caller.IsSuperUser);
        Assert.Equal("org-1", caller.OrganizationId);
        Assert.Contains("OrgAdmin", caller.Roles);
    }

    [Fact]
    public async Task NoCredentialProducesTheSystemActor()
    {
        // A principal naming itself "system" is an application like any other.
        var principal = Bearer("system", new Claim(ClaimTypes.Name, "system"));

        var caller = await new World().Factory().CreateAsync(Request(principal, CallerSide.System), TestContext.Current.CancellationToken);

        Assert.False(caller.IsSystem);
        Assert.False(caller.IsSuperUser);
    }

    [Fact]
    public async Task TheVersionBumperBumpsOnEveryRightsEvent()
    {
        var version = new Version();
        var bumper = new PermissionVersionBumper(version);

        await bumper.RoleCreatedAsync("r");
        await bumper.RoleUpdatedAsync("r");
        await bumper.RoleRemovedAsync("r");
        await bumper.DisabledAsync(null);
        await bumper.EnabledAsync(null);
        await bumper.DeletedAsync(null);

        Assert.Equal(7, version.Value);
    }

    // Access policies (docs/workflows.md › The member caller): a downstream module affects a
    // member's access in one organization without Members knowing why, by refusing portal
    // access there or narrowing the permissions; its verdicts are cached caller state, and a
    // policy write bumps the version.

    [Fact]
    public async Task APolicyRefusesPortalAccessForThatOrganizationOnly()
    {
        var world = new World();
        world.Contributors.Add(new OrganizationContributor());
        var policy = new SubscriptionPolicy { Unpaid = { "org-unpaid" } };
        world.Contributors.Add(policy);
        var factory = world.Factory();

        await Assert.ThrowsAsync<CallerDeniedException>(() =>
            factory.CreateAsync(Request(Signed("u-editor"), CallerSide.Member, "org-unpaid"), TestContext.Current.CancellationToken));
        var paid = await factory.CreateAsync(Request(Signed("u-editor"), CallerSide.Member, "org-paid"), TestContext.Current.CancellationToken);
        var staff = await factory.CreateAsync(Request(Signed("u-editor")), TestContext.Current.CancellationToken);

        Assert.Equal("org-paid", paid.OrganizationId);
        Assert.Contains("EditContent", staff.Permissions);
    }

    [Fact]
    public async Task APolicyNarrowsTheMembersPermissionsAndTheDecisionDenies()
    {
        var world = new World();
        world.Contributors.Add(new OrganizationContributor());
        world.Contributors.Add(new SubscriptionPolicy { Narrowed = { ["org-trial"] = ["PublishContent"] } });
        var factory = world.Factory();

        var trial = await factory.CreateAsync(Request(Signed("u-editor"), CallerSide.Member, "org-trial"), TestContext.Current.CancellationToken);
        var full = await factory.CreateAsync(Request(Signed("u-editor"), CallerSide.Member, "org-full"), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("PublishContent", trial.Permissions);
        Assert.False((await AccessWorld.Decision().DecideAsync(trial, "PublishContent", null, TestContext.Current.CancellationToken)).IsAllowed);
        Assert.True((await AccessWorld.Decision().DecideAsync(full, "PublishContent", null, TestContext.Current.CancellationToken)).IsAllowed);
        Assert.True((await AccessWorld.Decision().DecideAsync(trial, "EditContent", null, TestContext.Current.CancellationToken)).IsAllowed);
    }

    [Fact]
    public async Task APolicyVerdictChangedBetweenTwoRequestsReachesTheSecond()
    {
        var world = new World();
        world.Contributors.Add(new OrganizationContributor());
        var policy = new SubscriptionPolicy();
        world.Contributors.Add(policy);
        var factory = world.Factory();

        await factory.CreateAsync(Request(Signed("u-editor"), CallerSide.Member, "org-1"), TestContext.Current.CancellationToken);

        // The membership lapses: the policy write bumps the version.
        policy.Unpaid.Add("org-1");
        await world.Version.BumpAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<CallerDeniedException>(() =>
            factory.CreateAsync(Request(Signed("u-editor"), CallerSide.Member, "org-1"), TestContext.Current.CancellationToken));
    }

    /// <summary>A subscription module's policy: unpaid organizations lose portal access, trial
    /// organizations lose some permissions.</summary>
    private sealed class SubscriptionPolicy : ICallerContextContributor
    {
        public HashSet<string> Unpaid { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string[]> Narrowed { get; } = new(StringComparer.Ordinal);

        public Task ContributeAsync(CallerContextBuilder builder, CancellationToken cancellationToken = default)
        {
            if (builder.Request.Side != CallerSide.Member || builder.OrganizationId is not { } organization)
            {
                return Task.CompletedTask;
            }

            if (Unpaid.Contains(organization))
            {
                builder.DenialReason = $"The membership of {organization} is unpaid.";
            }
            else if (Narrowed.TryGetValue(organization, out var removed))
            {
                builder.Permissions.ExceptWith(removed);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class SideContributor : ICallerContextContributor
    {
        public Task ContributeAsync(CallerContextBuilder builder, CancellationToken cancellationToken = default)
        {
            if (builder.Request.Side == CallerSide.Admin)
            {
                builder.Permissions.Add("AdminOnly");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class DenyingContributor : ICallerContextContributor
    {
        public Task ContributeAsync(CallerContextBuilder builder, CancellationToken cancellationToken = default)
        {
            if (builder.Request.Side == CallerSide.Member)
            {
                builder.DenialReason = $"No binding to {builder.Request.RequestedOrganizationId}.";
            }

            return Task.CompletedTask;
        }
    }

    private sealed class OrganizationContributor : ICallerContextContributor
    {
        public Task ContributeAsync(CallerContextBuilder builder, CancellationToken cancellationToken = default)
        {
            if (builder.Request.RequestedOrganizationId is { } organization)
            {
                builder.OrganizationId = organization;
                builder.UserClass = CallerClasses.Member;
                builder.Roles.Add("OrgAdmin");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class SessionOrganization(string organization) : ICallerRequestResolver
    {
        public Task<CallerRequest> ResolveAsync(CallerRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(request.RequestedOrganizationId is null ? request with { RequestedOrganizationId = organization } : request);
    }
}
