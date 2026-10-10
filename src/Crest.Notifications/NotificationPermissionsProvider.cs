using Crest.Security.Permissions;

namespace Crest.Notifications;

public sealed class NotificationPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        NotificationPermissions.ManageNotifications,
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
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Moderator,
            Permissions = _allPermissions,
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Author,
            Permissions = _allPermissions,
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Contributor,
            Permissions = _allPermissions,
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Authenticated,
            Permissions = _allPermissions,
        },
    ];
}
