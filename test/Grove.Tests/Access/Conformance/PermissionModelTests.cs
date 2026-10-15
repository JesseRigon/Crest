using Grove.Access;
using Grove.Access.Services;

namespace Grove.Tests.Access.Conformance;

/// <summary>
/// The one resolution of the permission model (docs/permissions.md › Layers, Overrides):
/// group → role → set → permission, the most specific level that mentions a permission
/// deciding it, and a grant and a deny of one name at one level refused when written.
/// </summary>
public class PermissionModelTests
{
    private static readonly Dictionary<string, RoleDefinition> Roles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Editor"] = new("Editor", ["Publishing"], ["ViewAuditTrail"]),
        ["Viewer"] = new("Viewer", [], ["ViewContent"]),
        ["Accountant"] = new("Accountant", ["Ledgers"], []),
    };

    private static readonly Dictionary<string, IReadOnlyList<string>> Sets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Publishing"] = ["EditContent", "PublishContent"],
        ["Ledgers"] = ["ManageAccounts", "ManageTransactions"],
    };

    private static AccessGroupModel Group(string name, params string[] roles) =>
        new(name, name, null, AccessReach.Tenant, roles, ["u-1"], null);

    private static UserAccess Resolve(IReadOnlyList<AccessGroupModel> groups, IReadOnlyList<UserAccessOverride> overrides, params string[] direct) =>
        AccessResolution.Resolve("u-1", AccessReach.Tenant, 7, new AccessResolutionInput(
            groups, direct, overrides, name => Roles.GetValueOrDefault(name), name => Sets.GetValueOrDefault(name)));

    [Fact]
    public void A_user_holds_every_role_of_every_group_with_its_sets_and_loose_permissions()
    {
        var access = Resolve([Group("Writers", "Editor"), Group("Readers", "Viewer")], []);

        Assert.Equal(["Editor", "Viewer"], access.Roles.Order());
        Assert.Equal(["Publishing"], access.Sets);
        Assert.Equal(["EditContent", "PublishContent", "ViewAuditTrail", "ViewContent"], access.PermissionNames.Order());
        Assert.Equal("set:Publishing via role:Editor", access.Permissions.Single(grant => grant.Permission == "PublishContent").Source);
        Assert.Equal(7, access.PermissionVersion);
    }

    [Fact]
    public void A_permission_override_decides_whatever_the_groups_roles_and_sets_say()
    {
        var access = Resolve(
            [Group("Writers", "Editor")],
            [new(OverrideLevel.Permission, "PublishContent", OverrideEffect.Deny), new(OverrideLevel.Permission, "ManageMedia", OverrideEffect.Grant)]);

        Assert.DoesNotContain("PublishContent", access.PermissionNames);
        Assert.Contains("EditContent", access.PermissionNames);
        Assert.Equal(UserAccessGrant.OverrideSource, access.Permissions.Single(grant => grant.Permission == "ManageMedia").Source);
    }

    [Fact]
    public void A_set_override_and_a_role_override_act_at_their_level()
    {
        var setDenied = Resolve([Group("Writers", "Editor")], [new(OverrideLevel.Set, "Publishing", OverrideEffect.Deny)]);
        Assert.Equal(["ViewAuditTrail"], setDenied.PermissionNames);

        var roleGranted = Resolve([], [new(OverrideLevel.Role, "Accountant", OverrideEffect.Grant)]);
        Assert.Equal(["ManageAccounts", "ManageTransactions"], roleGranted.PermissionNames.Order());

        // A permission override is more specific than the set override above it.
        var mixed = Resolve([Group("Writers", "Editor")], [new(OverrideLevel.Set, "Publishing", OverrideEffect.Deny), new(OverrideLevel.Permission, "EditContent", OverrideEffect.Grant)]);
        Assert.Contains("EditContent", mixed.PermissionNames);
        Assert.DoesNotContain("PublishContent", mixed.PermissionNames);
    }

    [Fact]
    public void A_direct_role_assignment_counts_like_a_group()
    {
        Assert.Equal(["ViewContent"], Resolve([], [], "Viewer").PermissionNames);
    }

    [Fact]
    public void A_grant_and_a_deny_of_one_name_at_one_level_is_a_conflict()
    {
        Assert.NotNull(AccessResolution.Conflict([new(OverrideLevel.Permission, "EditContent", OverrideEffect.Grant), new(OverrideLevel.Permission, "editcontent", OverrideEffect.Deny)]));
        // The same name at two levels is not a conflict: the more specific one decides.
        Assert.Null(AccessResolution.Conflict([new(OverrideLevel.Role, "Editor", OverrideEffect.Deny), new(OverrideLevel.Permission, "Editor", OverrideEffect.Grant)]));
    }

    [Fact]
    public void A_reach_is_the_tenant_or_one_organization()
    {
        Assert.True(AccessReach.Tenant.IsTenant);
        Assert.Equal(string.Empty, AccessReach.Tenant.Key);
        Assert.Equal("org-1", AccessReach.FromKey("org-1").OrganizationId);
        Assert.True(AccessReach.FromKey(string.Empty).IsTenant);
    }
}
