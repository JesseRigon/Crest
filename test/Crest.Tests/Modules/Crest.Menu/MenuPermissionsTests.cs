using System.Security.Claims;
using Crest.Contents;
using Crest.Contents.Security;
using Crest.Menu;
using Crest.Security;
using Crest.Security.Permissions;
using MenuPermissions = Crest.Menu.Permissions;

namespace Crest.Tests.Modules.Crest.Menu;

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


        Assert.True(global::Crest.Tests.Access.ImpliedPermissions.IsGranted(MenuPermissions.ManageMenu,
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

        Assert.False(global::Crest.Tests.Access.ImpliedPermissions.IsGranted(requiredPermission,
            claims));
    }

    private static Permission CreateMenuPermission(Permission permission)
        => ContentTypePermissionsHelper.CreateDynamicPermission(
            ContentTypePermissionsHelper.ConvertToDynamicPermission(permission) ?? permission,
            "Menu");
}
