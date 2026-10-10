using Crest.Security.Permissions;

namespace Crest.Google;

public sealed class GoogleAnalyticsPermissionsProvider : IPermissionProvider
{
    [Obsolete("This will be removed in a future release. Instead use 'Crest.Google.Permissions.ManageGoogleAnalytics'.")]
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
