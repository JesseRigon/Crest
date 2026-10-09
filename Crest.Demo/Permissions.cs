using Crest.Security.Permissions;
using Crest.Users;

namespace Crest.Demo;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission DemoAPIAccess = new("DemoAPIAccess", "Access to Demo API ");
    public static readonly Permission ManageOwnUserProfile = new("ManageOwnUserProfile", "Manage own user profile", new Permission[] { UsersPermissions.ManageUsers });

    private static readonly IEnumerable<Permission> s_allPermissions =
    [
        DemoAPIAccess,
        ManageOwnUserProfile,
    ];

    private readonly IEnumerable<Permission> _generalPermissions =
    [
        ManageOwnUserProfile,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(s_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Authenticated,
            Permissions =
            [
                DemoAPIAccess,
            ],
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Editor,
            Permissions = _generalPermissions,
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Moderator,
            Permissions = _generalPermissions,
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Contributor,
            Permissions = _generalPermissions,
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Author,
            Permissions = _generalPermissions,
        },
    ];
}
