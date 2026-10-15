using System.Security.Claims;
using Grove.Documents;
using Grove.Access.Roles;
using Grove.Access.Roles.Models;
using Grove.Access.Roles.Services;
using Grove.Security;
using Grove.Security.Permissions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Grove.Tests.Modules.Grove.Roles.Sets;

// docs/permissions.md › Layers: a role holds permission sets and loose permissions; every
// reader of a role's claims sees both, the sets expanded, so existing roles keep working.
public class PermissionSetsTests
{
    [Fact]
    public async Task A_roles_claims_are_its_loose_permissions_and_its_sets()
    {
        var sets = new Mock<IPermissionSetService>();
        sets.Setup(service => service.ExpandAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> names, CancellationToken _) => names.Contains("Publishing")
                ? new HashSet<string>(["EditContent", "PublishContent"])
                : new HashSet<string>());
        var store = new RoleStore(
            Mock.Of<IServiceProvider>(),
            Mock.Of<ISystemRoleProvider>(),
            Mock.Of<IDocumentManager<RolesDocument>>(),
            sets.Object,
            Mock.Of<IStringLocalizer<RoleStore>>(),
            NullLogger<RoleStore>.Instance);

        var role = new Role { RoleName = "Editor", RoleClaims = [RoleClaim.Create("EditContent"), RoleClaim.Create("ViewAuditTrail")], PermissionSets = ["Publishing"] };
        var claims = await store.GetClaimsAsync(role, TestContext.Current.CancellationToken);

        Assert.Equal(["EditContent", "PublishContent", "ViewAuditTrail"], claims.Where(claim => claim.Type == Permission.ClaimType).Select(claim => claim.Value).Order());
        // A role without sets is read exactly as before.
        var plain = await store.GetClaimsAsync(new Role { RoleName = "Viewer", RoleClaims = [RoleClaim.Create("ViewContent")] }, TestContext.Current.CancellationToken);
        Assert.Equal(["ViewContent"], plain.Select(claim => claim.Value));
    }

    [Fact]
    public void A_cloned_role_keeps_its_sets()
    {
        var role = new Role { RoleName = "Editor", PermissionSets = ["Publishing"] };
        var clone = role.Clone();
        clone.PermissionSets.Add("Other");

        Assert.Equal(["Publishing"], role.PermissionSets);
        Assert.Equal(["Publishing", "Other"], clone.PermissionSets);
    }
}
