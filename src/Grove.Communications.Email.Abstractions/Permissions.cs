using Grove.Security.Permissions;

namespace Grove.Communications.Email;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        EmailPermissions.ManageEmailSettings,
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
