using Crest.Security.Permissions;

namespace Crest.Facebook;

public sealed class PixelPermissionProvider : IPermissionProvider
{
    public static readonly Permission ManageFacebookPixelPermission = FacebookConstants.ManageFacebookPixelPermission;

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageFacebookPixelPermission,
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
