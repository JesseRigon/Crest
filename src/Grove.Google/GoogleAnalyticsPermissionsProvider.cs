using Grove.Security.Permissions;

namespace Grove.Google;

public sealed class GoogleAnalyticsPermissionsProvider : IPermissionProvider
{
    [Obsolete("This will be removed in a future release. Instead use 'Grove.Google.Permissions.ManageGoogleAnalytics'.")]
    public static readonly Permission ManageGoogleAnalytics = Permissions.ManageGoogleAnalytics;

    private readonly IEnumerable<Permission> _allPermissions =
    [
        Permissions.ManageGoogleAnalytics,
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
