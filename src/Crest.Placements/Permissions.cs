using Crest.Security.Permissions;

namespace Crest.Placements;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManagePlacements = new("ManagePlacements", "Manage placements");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManagePlacements,
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
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Editor,
            Permissions = _allPermissions,
        },
    ];
}
