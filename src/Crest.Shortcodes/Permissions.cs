using Crest.Security.Permissions;

namespace Crest.Shortcodes;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        ShortcodesPermissions.ManageShortcodeTemplates,
    ];

    [Obsolete("This will be removed in a future release. Instead use 'ShortcodesPermissions.ManageShortcodeTemplates'.")]
    public static readonly Permission ManageShortcodeTemplates = ShortcodesPermissions.ManageShortcodeTemplates;

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Editor,
            Permissions = _allPermissions,
        },
    ];
}
