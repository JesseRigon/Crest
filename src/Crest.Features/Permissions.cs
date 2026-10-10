using Crest.Security.Permissions;

namespace Crest.Features;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        FeaturesPermissions.ManageFeatures,
    ];

    [Obsolete("This will be removed in a future release. Instead use 'FeaturesPermissions.ManageFeatures'.")]
    public static readonly Permission ManageFeatures = FeaturesPermissions.ManageFeatures;

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
