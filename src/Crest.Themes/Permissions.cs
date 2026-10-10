using Crest.Security.Permissions;

namespace Crest.Themes;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ApplyTheme = new("ApplyTheme", "Apply a Theme");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ApplyTheme,
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
