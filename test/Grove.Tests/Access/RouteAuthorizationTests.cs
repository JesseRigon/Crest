using Grove.Access;
using Grove.Access.Services;
using Grove.Routing;

namespace Grove.Tests.Access;

// The page gate in Access (docs/access.md, Pages): each admin page declares its permissions once
// in the page registry, a route admits a caller holding any one of them, the most specific
// matching template decides, and a path no page admits is denied. The admin menu asks the same
// service per item.
public class RouteAuthorizationTests
{
    private sealed class Decision : IAccessDecision
    {
        public Task<AccessDecision> DecideAsync(CallerContext caller, string permission, object resource = null, CancellationToken cancellationToken = default)
            => Task.FromResult(caller.Permissions.Contains(permission) ? AccessDecision.Allowed : AccessDecision.Denied("no"));
    }

    private sealed class Registry(params PageEntry[] entries) : IPageRegistry
    {
        private readonly PageSet _pages = new(entries);

        public Task<PageSet> GetDeclaredAsync() => Task.FromResult(_pages);

        public Task<PageSet> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(_pages);
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
        new Registry(
            new PageEntry { Id = "content-items", Routes = ["/Contents/ContentItems"], Permissions = ["ManageContent", "ListContent"] },
            new PageEntry { Id = "features", Routes = ["/Features"], Permissions = ["ManageFeatures"] },
            new PageEntry { Id = "undeclared", Routes = ["/Undeclared"], Component = typeof(object) },
            // A kind registry's generic page admits nothing itself; its source gates each kind.
            new PageEntry { Id = "party-type", Routes = ["/Parties/{Type}"], Component = typeof(object), DeclaredBySource = true },
            new PageEntry { Id = "parties-customers", Routes = ["/Parties/customers"], Permissions = ["ListContent"] },
            new PageEntry { Id = "parties-suppliers", Routes = ["/Parties/suppliers"], Permissions = ["ManageSuppliers"] },
            new PageEntry { Id = "site-page", Shell = ShellId.Site, Routes = ["/Features"], Permissions = ["ListContent"] }));

    [Fact]
    public async Task The_caller_gets_the_admin_routes_its_permissions_decide()
    {
        var routes = await Gate().GetAuthorizedRoutesAsync(Editor, TestContext.Current.CancellationToken);
        Assert.Equal(["/Contents/ContentItems", "/Parties/customers"], routes.Select(route => route.Template));
    }

    [Fact]
    public async Task A_path_is_decided_by_its_most_specific_page_and_anything_else_is_denied()
    {
        var gate = Gate();
        Assert.True(await gate.CanAccessAsync(Editor, "/Parties/Customers", TestContext.Current.CancellationToken));
        Assert.False(await gate.CanAccessAsync(Editor, "/Parties/suppliers", TestContext.Current.CancellationToken));
        Assert.False(await gate.CanAccessAsync(Editor, "/Parties/unknown", TestContext.Current.CancellationToken));
        Assert.False(await gate.CanAccessAsync(Editor, "/Features", TestContext.Current.CancellationToken));
        Assert.False(await gate.CanAccessAsync(Editor, "/Undeclared", TestContext.Current.CancellationToken));
        Assert.False(await gate.CanAccessAsync(Editor, "/Nowhere", TestContext.Current.CancellationToken));
        Assert.False(await gate.CanAccessAsync(null, "/Contents/ContentItems", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Any_one_declared_permission_admits()
    {
        var gate = Gate();
        Assert.True(await gate.CanAccessAsync(Editor, "/contents/contentitems", TestContext.Current.CancellationToken));
        Assert.True(await gate.AllowsAnyAsync(Editor, ["ManageFeatures", "ListContent"], TestContext.Current.CancellationToken));
        Assert.False(await gate.AllowsAnyAsync(Editor, [], TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Templates_match_segment_by_segment()
    {
        Assert.True(RouteTemplates.Matches("", ""));
        Assert.True(RouteTemplates.Matches("/", ""));
        Assert.True(RouteTemplates.Matches("/Users/Edit/{Id}", "/users/edit/42"));
        Assert.False(RouteTemplates.Matches("/Users/Edit/{Id}", "/Users/Edit"));
        Assert.Equal(2, RouteTemplates.Specificity("/Parties/customers"));
        Assert.Equal(1, RouteTemplates.Specificity("/Parties/{Type}"));
    }

    [Fact]
    public void A_duplicate_id_keeps_its_first_declaration()
    {
        var pages = new PageSet([
            new PageEntry { Id = "a", Routes = ["/A"], Permissions = ["One"] },
            new PageEntry { Id = "a", Routes = ["/B"], Permissions = ["Two"] }]);

        Assert.Single(pages.Entries);
        Assert.Null(pages.MatchRoute(ShellId.Admin, "/B"));
        Assert.Equal(["One"], pages.Find(ShellId.Admin, "a")!.Permissions);
    }
}
