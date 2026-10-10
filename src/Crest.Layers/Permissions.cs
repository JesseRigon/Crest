using Crest.Security.Permissions;

namespace Crest.Layers;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManageLayers = new("ManageLayers", "Manage layers");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageLayers,
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
