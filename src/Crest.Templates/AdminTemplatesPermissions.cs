using Crest.Security.Permissions;

namespace Crest.Templates;

public sealed class AdminTemplatesPermissions : IPermissionProvider
{
    public static readonly Permission ManageAdminTemplates = new("ManageAdminTemplates", "Manage admin templates", isSecurityCritical: true);

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageAdminTemplates,
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
