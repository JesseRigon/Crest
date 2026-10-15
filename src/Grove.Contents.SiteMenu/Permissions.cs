using Grove.Contents;
using Grove.Contents.Security;
using Grove.Security.Permissions;

namespace Grove.Contents.SiteMenu;

public sealed class Permissions : IPermissionProvider
{
    private static readonly Permission s_editMenuContent = ContentTypePermissionsHelper.CreateDynamicPermission(
        ContentTypePermissionsHelper.PermissionTemplates[CommonPermissions.EditContent.Name],
        "Menu");

    public static readonly Permission ManageMenu = new("ManageMenu", "Manage menus", [s_editMenuContent]);

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageMenu,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
