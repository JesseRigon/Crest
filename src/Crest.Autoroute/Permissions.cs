using Crest.Security.Permissions;

namespace Crest.Autoroute;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        AutoroutePermissions.SetHomepage,
    ];

    [Obsolete("This will be removed in a future release. Instead use 'AutoroutePermissions.SetHomepage'.")]
    public static readonly Permission SetHomepage = AutoroutePermissions.SetHomepage;

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
