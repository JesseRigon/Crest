using Grove.Security.Permissions;

namespace Grove.Sharing;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManageTenantSharing = new(SharingPermissionNames.ManageTenantSharing, "Manage tenant-level sharing entries and rules", isSecurityCritical: true);
    public static readonly Permission ShareWithAnyone = new(SharingPermissionNames.ShareWithAnyone, "Share items with anyone (make them public)", isSecurityCritical: true);

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageTenantSharing,
        ShareWithAnyone,
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
