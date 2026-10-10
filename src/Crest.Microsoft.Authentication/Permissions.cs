using Crest.Security.Permissions;

namespace Crest.Microsoft.Authentication;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManageMicrosoftAuthentication
        = new("ManageMicrosoftAuthentication", "Manage Microsoft Authentication settings");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageMicrosoftAuthentication,
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
