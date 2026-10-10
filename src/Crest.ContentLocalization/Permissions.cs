using Crest.Security.Permissions;

namespace Crest.ContentLocalization;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        ContentLocalizationPermissions.LocalizeContent,
        ContentLocalizationPermissions.LocalizeOwnContent,
        ContentLocalizationPermissions.ManageContentCulturePicker,
    ];

    private readonly IEnumerable<Permission> _generalPermissions =
    [
        ContentLocalizationPermissions.LocalizeOwnContent,
    ];

    [Obsolete("This will be removed in a future release. Instead use 'ContentLocalizationPermissions.LocalizeContent'.")]
    public static readonly Permission LocalizeContent = ContentLocalizationPermissions.LocalizeContent;

    [Obsolete("This will be removed in a future release. Instead use 'ContentLocalizationPermissions.LocalizeOwnContent'.")]
    public static readonly Permission LocalizeOwnContent = ContentLocalizationPermissions.LocalizeOwnContent;

    [Obsolete("This will be removed in a future release. Instead use 'ContentLocalizationPermissions.ManageContentCulturePicker'.")]
    public static readonly Permission ManageContentCulturePicker = ContentLocalizationPermissions.ManageContentCulturePicker;

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
