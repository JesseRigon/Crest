using Grove.Security.Permissions;

namespace Grove.Data.Connectors;

/// <summary>The connector engine's permissions: managing connections, using them, connecting one's own accounts.</summary>
public sealed class Permissions : IPermissionProvider
{
    public Task<IEnumerable<Permission>> GetPermissionsAsync() => Task.FromResult<IEnumerable<Permission>>(ConnectorsPermissions.All);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Administrator,
            Permissions = ConnectorsPermissions.All,
        },
    ];
}
