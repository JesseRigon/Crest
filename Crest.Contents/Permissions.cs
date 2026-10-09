using Crest.Security.Permissions;

namespace Crest.Contents;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _readerPermissions =
    [
        CommonPermissions.ViewContent,
    ];

    private readonly IEnumerable<Permission> _allPermissions =
    [
        CommonPermissions.EditContent,
        CommonPermissions.EditOwnContent,
        CommonPermissions.PublishContent,
        CommonPermissions.PublishOwnContent,
        CommonPermissions.DeleteContent,
        CommonPermissions.DeleteOwnContent,
        CommonPermissions.ViewContent,
        CommonPermissions.ViewOwnContent,
        CommonPermissions.PreviewContent,
        CommonPermissions.PreviewOwnContent,
        CommonPermissions.CloneContent,
        CommonPermissions.CloneOwnContent,
        CommonPermissions.AccessContentApi,
        CommonPermissions.ListContent,
        CommonPermissions.EditContentOwner,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Administrator,
            Permissions =
            [
                CommonPermissions.PublishContent,
                CommonPermissions.EditContent,
                CommonPermissions.DeleteContent,
                CommonPermissions.PreviewContent,
                CommonPermissions.CloneContent,
                CommonPermissions.AccessContentApi,
                CommonPermissions.ListContent,
                CommonPermissions.EditContentOwner,
            ],
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Editor,
            Permissions =
            [
                CommonPermissions.PublishContent,
                CommonPermissions.EditContent,
                CommonPermissions.DeleteContent,
                CommonPermissions.PreviewContent,
                CommonPermissions.CloneContent,
                CommonPermissions.ListContent,
            ],
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Author,
            Permissions =
            [
                CommonPermissions.PublishOwnContent,
                CommonPermissions.EditOwnContent,
                CommonPermissions.DeleteOwnContent,
                CommonPermissions.PreviewOwnContent,
                CommonPermissions.CloneOwnContent,
            ],
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Contributor,
            Permissions =
            [
                CommonPermissions.EditOwnContent,
                CommonPermissions.PreviewOwnContent,
                CommonPermissions.CloneOwnContent,
            ],
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Authenticated,
            Permissions = _readerPermissions,
        },
        new PermissionStereotype
        {
            Name = PlatformConstants.Roles.Anonymous,
            Permissions = _readerPermissions,
        },
    ];
}
