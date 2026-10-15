using System.Security.Claims;
using Grove.Contents;
using Grove.Contents.Security;
using Grove.Contents.SiteMenu;
using Grove.Security;
using Grove.Security.Permissions;
using MenuPermissions = Grove.Contents.SiteMenu.Permissions;

namespace Grove.Tests.Modules.Grove.Menu;

public class MenuPermissionsTests
{
    [Theory]
    [InlineData(nameof(CommonPermissions.EditContent))]
    [InlineData("Edit_Menu")]
    public void ManageMenu_EditPermission_GrantsManageMenu(string grantedPermission)
    {
        var claims = new[]
        {
            new Claim(Permission.ClaimType, grantedPermission),
        };


        Assert.True(global::Grove.Tests.Access.ImpliedPermissions.IsGranted(MenuPermissions.ManageMenu,
            claims));
    }

    [Theory]
    [InlineData(nameof(CommonPermissions.DeleteContent))]
    [InlineData(nameof(CommonPermissions.PublishContent))]
    [InlineData(nameof(CommonPermissions.EditContentOwner))]
    public void EditMenuPermission_UnrelatedOperation_RemainsDenied(string permissionName)
    {
        var editMenu = CreateMenuPermission(CommonPermissions.EditContent);
        var claims = new[]
        {
            new Claim(Permission.ClaimType, editMenu.Name),
        };

        var requiredPermission = CreateMenuPermission(
            ContentTypePermissionsHelper.PermissionTemplates[permissionName]);

        Assert.False(global::Grove.Tests.Access.ImpliedPermissions.IsGranted(requiredPermission,
            claims));
    }

    private static Permission CreateMenuPermission(Permission permission)
        => ContentTypePermissionsHelper.CreateDynamicPermission(
            ContentTypePermissionsHelper.ConvertToDynamicPermission(permission) ?? permission,
            "Menu");
}
