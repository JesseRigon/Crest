using Grove.Security.Permissions;

namespace Grove.Routes;

public static class RoutesPermissions
{
    /// <summary>Opens the route management page: the page registry's routes, the content routes and the home route.</summary>
    public static readonly Permission ViewRoutes = new("ViewRoutes", "View the tenant's routes");
}
