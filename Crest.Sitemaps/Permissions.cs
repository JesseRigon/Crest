using Crest.Security.Permissions;

namespace Crest.Sitemaps;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        SitemapsPermissions.ManageSitemaps,
    ];

    [Obsolete("This will be removed in a future release. Instead use 'SitemapsPermissions.ManageSitemaps'.")]
    public static readonly Permission ManageSitemaps = SitemapsPermissions.ManageSitemaps;

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
