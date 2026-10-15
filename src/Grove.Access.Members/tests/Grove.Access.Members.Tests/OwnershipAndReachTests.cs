using Grove.Access;
using Grove.Access.Members.Constants;
using Grove.Access.Members.Models;
using Grove.Access.Members.Services;
using Grove.Parties.Services;
using Xunit;

namespace Grove.Access.Members.Tests;

// docs/permissions.md › Who manages the permission model (the hierarchy bound) and › The
// organization owner (owner-locked permissions).
public class OwnershipAndReachTests
{
    private sealed class Hierarchy(Dictionary<(string, string), string[]> subtrees) : IUserHierarchyService
    {
        public Task<IReadOnlyList<string>> GetSubtreeUserIdsAsync(string organizationId, string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(subtrees.TryGetValue((organizationId, userId), out var below) ? [userId, .. below] : [userId]);

        public Task EnsureNodeAsync(string organizationId, string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveNodeAsync(string organizationId, string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddManagerAsync(string organizationId, string reportId, string managerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveManagerAsync(string organizationId, string reportId, string managerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<HierarchyNodeModel>> GetNodesAsync(string organizationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<IReadOnlyList<string>>> GetChainsAsync(string organizationId, string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<HierarchyNodeModel>> GetNodesForUserAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RebuildAsync(string organizationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Tenant : ITenantOrganizationService
    {
        public Task<string?> GetIdAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("org-tenant");
        public Task<bool> IsTenantOrganizationAsync(string? organizationId, CancellationToken cancellationToken = default) => Task.FromResult(organizationId == "org-tenant");
        public Task<string> EnsureAsync(CancellationToken cancellationToken = default) => Task.FromResult("org-tenant");
    }

    private sealed class Owners(Dictionary<string, string> owners) : IOrganizationOwnership
    {
        public string OwnerRoleName => MemberRoleTemplates.MemberAdministrator;
        public Task<string?> GetOwnerAsync(string organizationId, CancellationToken cancellationToken = default) => Task.FromResult(owners.GetValueOrDefault(organizationId));
        public Task<bool> EnsureOwnerAsync(string organizationId, string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task TransferAsync(string organizationId, string newOwnerUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Defaults(params string[] locked) : IOrganizationAccessDefaultsService
    {
        public Task<OrganizationAccessDefaults> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(new OrganizationAccessDefaults { OwnerLockedPermissions = [.. locked] });
        public Task SetAsync(OrganizationAccessDefaults defaults, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Members : IMemberService
    {
        public Task<MemberModel?> GetAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<MemberModel>> ListByOrganizationAsync(string organizationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MemberModel>>(organizationId == "org-a"
                ? [new("owner-a", "owner", null, null, []), new("admin-a", "admin", null, null, []), new("m-1", "m1", null, null, []), new("m-2", "m2", null, null, [])]
                : []);

        public Task<Grove.Access.Users.IUser?> CreateMemberAsync(CreateMemberRequest request, Action<string, string> reportError, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Decision(params string[] granted) : IAccessDecision
    {
        public Task<AccessDecision> DecideAsync(CallerContext caller, string permission, object? resource = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(granted.Contains(permission) ? AccessDecision.Allowed : AccessDecision.Denied("no"));
    }

    private static MemberHierarchyReach Reach(IAccessDecision? decision = null) => new(
        null!,
        new Hierarchy(new()
        {
            [("org-a", "admin-a")] = ["owner-a", "m-1", "m-2"],
            [("org-a", "m-1")] = ["m-2"],
            [("org-tenant", "staff-lead")] = ["staff-1"],
        }),
        new Tenant(),
        new Owners(new() { ["org-a"] = "owner-a" }),
        new Members(),
        decision);

    private static CallerContext Member(string userId, string organizationId) =>
        new() { Tenant = "t", Side = CallerSide.Member, UserId = userId, UserClass = UserClasses.Member, OrganizationId = organizationId };

    [Fact]
    public async Task A_manager_manages_the_users_under_them_never_themself_nor_the_owner()
    {
        var managed = await Reach().ManagedUsersAsync(Member("admin-a", "org-a"), AccessReach.Organization("org-a"), TestContext.Current.CancellationToken);
        Assert.Equal(["m-1", "m-2"], managed!.Order());

        var below = await Reach().ManagedUsersAsync(Member("m-1", "org-a"), AccessReach.Organization("org-a"), TestContext.Current.CancellationToken);
        Assert.Equal(["m-2"], below);
    }

    [Fact]
    public async Task No_position_no_management_and_the_shell_bounds_the_reach()
    {
        Assert.Empty((await Reach().ManagedUsersAsync(Member("m-2", "org-a"), AccessReach.Organization("org-a"), TestContext.Current.CancellationToken))!);
        // Acting in organization A manages nothing in B, nor at the tenant reach.
        Assert.Empty((await Reach().ManagedUsersAsync(Member("admin-a", "org-a"), AccessReach.Organization("org-b"), TestContext.Current.CancellationToken))!);
        Assert.Empty((await Reach().ManagedUsersAsync(Member("admin-a", "org-a"), AccessReach.Tenant, TestContext.Current.CancellationToken))!);
    }

    [Fact]
    public async Task Staff_manage_under_them_in_the_tenant_organizations_hierarchy()
    {
        var staff = new CallerContext { Tenant = "t", Side = CallerSide.Admin, UserId = "staff-lead", UserClass = UserClasses.Staff };
        Assert.Equal(["staff-1"], await Reach().ManagedUsersAsync(staff, AccessReach.Tenant, TestContext.Current.CancellationToken));
        Assert.Empty((await Reach().ManagedUsersAsync(staff, AccessReach.Organization("org-a"), TestContext.Current.CancellationToken))!);
    }

    [Fact]
    public async Task Staff_holding_the_organization_members_permission_manage_every_organizations_members_but_its_owner()
    {
        // Staff over organizations (ruling 2026-10-14): no position in the organization's hierarchy needed.
        var staff = new CallerContext { Tenant = "t", Side = CallerSide.Admin, UserId = "staff-1", UserClass = UserClasses.Staff };
        var managed = await Reach(new Decision(OrganizationPermissionNames.ManageOrgMembers)).ManagedUsersAsync(staff, AccessReach.Organization("org-a"), TestContext.Current.CancellationToken);
        Assert.Equal(["admin-a", "m-1", "m-2"], managed!.Order());

        // Without the permission, or acting in an organization, the hierarchy still bounds.
        Assert.Empty((await Reach(new Decision()).ManagedUsersAsync(staff, AccessReach.Organization("org-a"), TestContext.Current.CancellationToken))!);
        var below = await Reach(new Decision(OrganizationPermissionNames.ManageOrgMembers)).ManagedUsersAsync(Member("m-1", "org-a"), AccessReach.Organization("org-a"), TestContext.Current.CancellationToken);
        Assert.Equal(["m-2"], below);
    }

    [Fact]
    public async Task The_super_user_is_not_narrowed()
    {
        var super = new CallerContext { Tenant = "t", Side = CallerSide.Admin, UserId = "root", IsSuperUser = true };
        Assert.True(await ((IUserManagementReach)Reach()).ManagesAsync(super, AccessReach.Organization("org-a"), "owner-a", TestContext.Current.CancellationToken));
        Assert.False(await ((IUserManagementReach?)null).ManagesAsync(Member("admin-a", "org-a"), AccessReach.Organization("org-a"), "m-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Owner_locked_permissions_are_the_owners_alone()
    {
        var ceiling = new OwnerLockedCeiling(new Defaults("ManagePayments"), new Owners(new() { ["org-a"] = "owner-a" }));

        Assert.Null(ceiling.Deny(Member("owner-a", "org-a"), "ManagePayments"));
        Assert.NotNull(ceiling.Deny(Member("admin-a", "org-a"), "ManagePayments"));
        Assert.Null(ceiling.Deny(Member("admin-a", "org-a"), "ViewContent"));
        // Staff at tenant reach are not in an organization: the lock is an organization's.
        Assert.Null(ceiling.Deny(new CallerContext { Tenant = "t", Side = CallerSide.Admin, UserId = "staff-1" }, "ManagePayments"));
    }
}
