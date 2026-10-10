using Crest.Security.Permissions;

namespace Crest.Email;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        EmailPermissions.ManageEmailSettings,
    ];

    [Obsolete("This will be removed in a future release. Instead use 'EmailPermissions.ManageEmailSettings'.")]
    public static readonly Permission ManageEmailSettings = EmailPermissions.ManageEmailSettings;

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
