using Crest.Security.Permissions;

namespace Crest.Media;

public sealed class MediaCachePermissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        MediaPermissions.ManageAssetCache,
    ];

    [Obsolete("This will be removed in a future release. Instead use 'MediaPermissions.ManageAssetCache'.")]
    public static readonly Permission ManageAssetCache = MediaPermissions.ManageAssetCache;

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
