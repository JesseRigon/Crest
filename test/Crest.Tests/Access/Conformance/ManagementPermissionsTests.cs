using Crest.Access;

namespace Crest.Tests.Access.Conformance;

/// <summary>
/// Who manages the permission model (docs/permissions.md, ruling 2026-10-13): two shipped sets
/// of CRUD permissions, asked against an organization at its reach - the same permission at a
/// different reach, decided by the caller's acting organization.
/// </summary>
public class ManagementPermissionsTests
{
    [Fact]
    public void Two_default_sets_carry_the_crud_permissions()
    {
        var sets = new AccessPermissionSetProvider().GetDefaultSets().ToDictionary(set => set.Name);

        Assert.Contains("CreateRoles", sets[AccessPermissions.ManagePermissionsSet].Permissions);
        Assert.Contains("ReadPermissionCatalog", sets[AccessPermissions.ManagePermissionsSet].Permissions);
        Assert.Contains("UpdateUserOverrides", sets[AccessPermissions.ManagePermissionsSet].Permissions);
        Assert.Contains("DeleteGroupMembership", sets[AccessPermissions.ManageGroupsSet].Permissions);
        Assert.DoesNotContain("CreateGroups", sets[AccessPermissions.ManagePermissionsSet].Permissions);
    }

    [Fact]
    public async Task An_organizations_management_is_decided_by_the_acting_organization()
    {
        var mapper = new OrganizationReachPermissionMapper();
        var staff = new CallerContext { Tenant = "t", Side = CallerSide.Admin, UserId = "s" };
        var memberOfA = new CallerContext { Tenant = "t", Side = CallerSide.Member, UserId = "m", OrganizationId = "org-a" };
        var cancellation = TestContext.Current.CancellationToken;

        Assert.Equal(["CreateGroups"], (await mapper.MapAsync("CreateGroups", new OrganizationResource("org-b"), staff, cancellation))!.Select(candidate => candidate.Permission));
        Assert.Equal(["CreateGroups"], (await mapper.MapAsync("CreateGroups", new OrganizationResource("org-a"), memberOfA, cancellation))!.Select(candidate => candidate.Permission));
        Assert.Empty((await mapper.MapAsync("CreateGroups", new OrganizationResource("org-b"), memberOfA, cancellation))!);
        Assert.Null(await mapper.MapAsync("ViewContent", new OrganizationResource("org-a"), memberOfA, cancellation));
    }
}
