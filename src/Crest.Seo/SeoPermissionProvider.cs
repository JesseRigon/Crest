using Crest.Security.Permissions;

namespace Crest.Seo;

public sealed class SeoPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        SeoPermissions.ManageSeoSettings,
    ];

    [Obsolete("This will be removed in a future release. Instead use 'SeoPermissions.ManageSeoSettings'.")]
    public static readonly Permission ManageSeoSettings = SeoPermissions.ManageSeoSettings;

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
