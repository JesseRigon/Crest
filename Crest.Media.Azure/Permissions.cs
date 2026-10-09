using Crest.Security.Permissions;

namespace Crest.Media.Azure;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ViewAzureMediaOptions = new("ViewAzureMediaOptions", "View Azure Media Options");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ViewAzureMediaOptions,
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
