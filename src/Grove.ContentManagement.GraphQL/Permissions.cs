using Grove.Apis.GraphQL;
using Grove.Security.Permissions;

namespace Grove.ContentManagement.GraphQL;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        GraphQLPermissions.ApiViewContent,
    ];

    [Obsolete("This will be removed in a future release. Instead use 'GraphQLPermissions.ApiViewContent'.")]
    public static readonly Permission ApiViewContent = GraphQLPermissions.ApiViewContent;

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
