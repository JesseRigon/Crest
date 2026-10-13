using Crest.Access;
using Crest.Access.Services;

namespace Crest.Tests.Access;

// The page gate in Access (docs/access.md, Pages): every module registers its routes, the
// first registration of a template wins, a route is decided for the caller by the one
// decision, and a path no route matches is denied.
public class RouteAuthorizationTests
{
    private sealed class Decision : IAccessDecision
    {
        public Task<AccessDecision> DecideAsync(CallerContext caller, string permission, object resource = null, CancellationToken cancellationToken = default)
            => Task.FromResult(caller.Permissions.Contains(permission) ? AccessDecision.Allowed : AccessDecision.Denied("no"));
    }

    private sealed class Routes(params RoutePermission[] routes) : IRoutePermissionProvider
    {
        public IEnumerable<RoutePermission> GetRoutes() => routes;
    }

    private static readonly CallerContext Editor = new()
    {
        Tenant = "t",
        Side = CallerSide.Admin,
        UserId = "u-1",
        Permissions = new HashSet<string>(["ListContent"], StringComparer.OrdinalIgnoreCase),
    };

    private static RouteAuthorizationService Gate() => new(
        new Decision(),
        [new Routes(new("/Contents/ContentItems", "ListContent"), new("/Features", "ManageFeatures")), new Routes(new("/features", "ListContent"), new("/Parties/{Type}", "ListContent"))]);

    [Fact]
    public async Task The_caller_gets_the_routes_its_permissions_decide()
    {
        var routes = await Gate().GetAuthorizedRoutesAsync(Editor, TestContext.Current.CancellationToken);
        Assert.Equal(["/Contents/ContentItems", "/Parties/{Type}"], routes.Select(route => route.Template));
    }

    [Fact]
    public async Task A_path_is_decided_by_the_first_template_and_an_unknown_path_is_denied()
    {
        var gate = Gate();
        Assert.True(await gate.CanAccessAsync(Editor, "/Parties/Person", TestContext.Current.CancellationToken));
        Assert.False(await gate.CanAccessAsync(Editor, "/Features", TestContext.Current.CancellationToken));
        Assert.False(await gate.CanAccessAsync(Editor, "/Nowhere", TestContext.Current.CancellationToken));
        Assert.False(await gate.CanAccessAsync(null, "/Contents/ContentItems", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Templates_match_segment_by_segment()
    {
        Assert.True(RouteTemplates.Matches("", ""));
        Assert.True(RouteTemplates.Matches("/Users/Edit/{Id}", "/users/edit/42"));
        Assert.False(RouteTemplates.Matches("/Users/Edit/{Id}", "/Users/Edit"));
    }
}
