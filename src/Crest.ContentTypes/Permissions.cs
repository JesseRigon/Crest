using Crest.Contents;
using Crest.Security.Permissions;

namespace Crest.ContentTypes;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        ContentTypesPermissions.ViewContentTypes,
        ContentTypesPermissions.EditContentTypes,
    ];

    [Obsolete("This will be removed in a future release. Instead use 'ContentTypesPermissions.ViewContentTypes'.")]
    public static readonly Permission ViewContentTypes = ContentTypesPermissions.ViewContentTypes;

    [Obsolete("This will be removed in a future release. Instead use 'ContentTypesPermissions.EditContentTypes'.")]
    public static readonly Permission EditContentTypes = ContentTypesPermissions.EditContentTypes;

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
