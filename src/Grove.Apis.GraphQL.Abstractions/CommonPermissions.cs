using Grove.Security.Permissions;

namespace Grove.Apis.GraphQL;

[Obsolete("This class will be removed in a future release, please use GraphQLPermissions instead.")]
public static class CommonPermissions
{
    public static readonly Permission ExecuteGraphQLMutations = GraphQLPermissions.ExecuteGraphQLMutations;

    public static readonly Permission ExecuteGraphQL = GraphQLPermissions.ExecuteGraphQL;
}
