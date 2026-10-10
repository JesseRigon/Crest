using Crest.Security.Permissions;

namespace Crest.AdminDashboard;

public sealed class Permissions : IPermissionProvider
{
    public static readonly Permission ManageAdminDashboard = new("ManageAdminDashboard", "Manage the Admin Dashboard");
    public static readonly Permission AccessAdminDashboard = new("AccessAdminDashboard", "Access the Admin Dashboard", new[] { ManageAdminDashboard });

    private readonly IEnumerable<Permission> _allPermissions =
    [
        AccessAdminDashboard,
        ManageAdminDashboard,
    ];

    private readonly IEnumerable<Permission> _generalPermissions =
    [
        AccessAdminDashboard,
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
            Permissions = _generalPermissions,
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Moderator,
            Permissions = _generalPermissions,
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Author,
            Permissions = _generalPermissions,
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Contributor,
            Permissions = _generalPermissions,
        },
    ];
}
