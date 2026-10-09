using System.Security.Claims;
using Microsoft.Extensions.Localization;
using Moq;
using Crest.Contents;
using Crest.Contents.Security;
using Crest.Menu;
using Crest.Navigation;
using Crest.Security;
using Crest.Security.Permissions;
using MenuAdminMenu = Crest.Menu.AdminMenu;
using MenuPermissions = Crest.Menu.Permissions;

namespace Crest.Tests.Modules.Crest.Menu;

public class MenuPermissionsTests
{
    [Fact]
    public async Task BuildNavigationAsync_ListMenuPermission_UsesMenuResource()
    {
        var localizer = new Mock<IStringLocalizer<MenuAdminMenu>>();
        localizer
            .Setup(x => x[It.IsAny<string>()])
            .Returns((string name) => new LocalizedString(name, name));

        var builder = new NavigationBuilder();

        await new MenuAdminMenu(localizer.Object).BuildNavigationAsync(NavigationConstants.AdminId, builder);

        var contentMenu = Assert.Single(builder.Build());
        var menusItem = Assert.Single(contentMenu.Items);
        var permission = Assert.Single(menusItem.Permissions);

        Assert.Equal("ListContent_Menu", permission.Name);
        Assert.Equal("Menu", menusItem.Resource);
    }

    [Theory]
    [InlineData(nameof(CommonPermissions.EditContent))]
    [InlineData("Edit_Menu")]
    public void ManageMenu_EditPermission_GrantsManageMenu(string grantedPermission)
    {
        var claims = new[]
        {
            new Claim(Permission.ClaimType, grantedPermission),
        };

        var permissionGrantingService = new DefaultPermissionGrantingService();

        Assert.True(permissionGrantingService.IsGranted(
            new PermissionRequirement(MenuPermissions.ManageMenu),
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

        var permissionGrantingService = new DefaultPermissionGrantingService();
        var requiredPermission = CreateMenuPermission(
            ContentTypePermissionsHelper.PermissionTemplates[permissionName]);

        Assert.False(permissionGrantingService.IsGranted(
            new PermissionRequirement(requiredPermission),
            claims));
    }

    private static Permission CreateMenuPermission(Permission permission)
        => ContentTypePermissionsHelper.CreateDynamicPermission(
            ContentTypePermissionsHelper.ConvertToDynamicPermission(permission) ?? permission,
            "Menu");
}
