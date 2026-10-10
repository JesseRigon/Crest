using Crest.Security.Permissions;

namespace Crest.Google;

public sealed class GoogleAuthenticationPermissionProvider : IPermissionProvider
{
    public static readonly Permission ManageGoogleAuthentication = Permissions.ManageGoogleAuthentication;

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageGoogleAuthentication,
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
