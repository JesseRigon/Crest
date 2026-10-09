using Crest.Security.Permissions;

namespace Crest.Media;

public sealed class PermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        MediaPermissions.ManageMedia,
        MediaPermissions.ManageMediaFolder,
        MediaPermissions.ManageOthersMedia,
        MediaPermissions.ManageOwnMedia,
        MediaPermissions.ManageAttachedMediaFieldsFolder,
        MediaPermissions.UploadRestrictedMedia,
        MediaPermissions.ManageMediaProfiles,
        MediaPermissions.ViewMediaOptions,
        MediaPermissions.ManageMediaApiSettings,
    ];

    private readonly IEnumerable<Permission> _generalPermissions =
    [
        MediaPermissions.ManageOwnMedia,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync() => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Administrator,
            Permissions =
            [
                MediaPermissions.ManageMediaFolder,
                MediaPermissions.UploadRestrictedMedia,
                MediaPermissions.ManageMediaProfiles,
                MediaPermissions.ViewMediaOptions,
                MediaPermissions.ManageMediaApiSettings,
            ],
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Editor,
            Permissions =
            [
                MediaPermissions.ManageMedia,
                MediaPermissions.ManageOwnMedia,
                MediaPermissions.UploadRestrictedMedia,
            ],
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
