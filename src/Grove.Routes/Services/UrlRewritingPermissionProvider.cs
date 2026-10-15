using Grove.Security.Permissions;

namespace Grove.Routes.Services;

public sealed class UrlRewritingPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        RoutesPermissions.ViewRoutes,
        UrlRewritingPermissions.ManageUrlRewritingRules,
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
