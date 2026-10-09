using Crest.Security.Permissions;

namespace Crest.Facebook;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManageFacebookApp = new("ManageFacebookApp", "View and edit the Facebook app.");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageFacebookApp,
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
