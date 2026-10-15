using Grove.Security.Permissions;

namespace Grove.Apis.GraphQL;

public class GraphQLPermissionContext
{
    public Permission Permission { get; set; }
    public object Resource { get; set; }

    public GraphQLPermissionContext(Permission permission, object resource = null)
    {
        Permission = permission;
        Resource = resource;
    }
}
