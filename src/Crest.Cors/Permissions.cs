using Crest.Security.Permissions;

namespace Crest.Cors;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManageCorsSettings = new("ManageCorsSettings", "Managing Cors Settings", isSecurityCritical: true);


    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageCorsSettings,
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
