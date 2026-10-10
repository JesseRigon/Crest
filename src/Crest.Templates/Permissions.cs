using Crest.Security.Permissions;

namespace Crest.Templates;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManageTemplates = new("ManageTemplates", "Manage templates", isSecurityCritical: true);

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageTemplates,
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
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Editor,
            Permissions = _allPermissions,
        },
    ];
}
