using Crest.Security.Permissions;

namespace Crest.Https;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManageHttps = new("ManageHttps", "Manage HTTPS");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageHttps,
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
