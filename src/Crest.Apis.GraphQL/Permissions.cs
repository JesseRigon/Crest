using Crest.Security.Permissions;

namespace Crest.Apis.GraphQL;

public sealed class Permissions : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        GraphQLPermissions.ExecuteGraphQL,
        GraphQLPermissions.ExecuteGraphQLMutations,
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
                GraphQLPermissions.ExecuteGraphQLMutations,
            ],
        },
    ];
}
