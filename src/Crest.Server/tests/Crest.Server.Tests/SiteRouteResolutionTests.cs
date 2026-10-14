using Crest.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Crest.Server.Tests;

// One routing system (ruling 2026-10-13, docs/shells-and-themes.md › Shell dispatch): the site
// shell resolves every page request through its route table and forwards a legacy-owned path
// to MVC; the content page is "/" only.
public sealed class SiteRouteResolutionTests
{
    private sealed class ContentPage : Microsoft.AspNetCore.Components.ComponentBase;

    private sealed class ShopPage : Microsoft.AspNetCore.Components.ComponentBase;

    private static RouteComponentTable Table() => new(
    [
        new RouteComponentEntry("/", typeof(ContentPage), ShellId.Site, IsDefaultLanding: true),
        new RouteComponentEntry("/shop/{id}", typeof(ShopPage), ShellId.Site),
        // A designed page: the content page's component under its own route.
        new RouteComponentEntry("/landing", typeof(ContentPage), ShellId.Site, AllowsAnonymous: true),
        // An admin page sharing a literal never counts for the site.
        new RouteComponentEntry("/features", typeof(ShopPage), ShellId.Admin),
    ]);

    [Fact]
    public void AModuleSitePageIsServedAtItsLiteral()
    {
        var resolution = SiteRouteResolver.Resolve(Table(), "/shop/42", () => throw new InvalidOperationException("not asked"));

        Assert.Equal(SiteRouteKind.ModulePage, resolution.Kind);
        Assert.Equal("/shop/42", resolution.Path.Value);
    }

    [Theory]
    [InlineData("/landing")]
    [InlineData("/about-us")]
    [InlineData("/features")]
    [InlineData("/")]
    public void DesignedPagesAndContentPathsAreTheContentPage(string path)
    {
        var resolution = SiteRouteResolver.Resolve(Table(), path, () => false);

        Assert.Equal(SiteRouteKind.ContentPage, resolution.Kind);
        Assert.Equal("/", resolution.Path.Value);
    }

    [Theory]
    [InlineData("/landing")]
    [InlineData("/Error/404")]
    public void ALegacyOwnedPathPassesThrough(string path)
    {
        var resolution = SiteRouteResolver.Resolve(Table(), path, () => true);

        Assert.Equal(SiteRouteKind.Legacy, resolution.Kind);
        Assert.Equal(path, resolution.Path.Value);
    }

    [Theory]
    [InlineData("/Crest.Resources/Styles/site.css", false)]
    [InlineData("/TheAdmin/js/admin.js", false)]
    [InlineData("/about", true)]
    [InlineData("/", true)]
    public void OnlyPageRequestsAreResolved(string path, bool isPage) =>
        Assert.Equal(isPage, Crest.Middlewares.ShellSelectionMiddleware.IsPageRequest(path));
}

public sealed class LegacyRouteOwnershipTests
{
    private static RouteEndpoint Endpoint(string pattern, object? defaults = null, object? requiredValues = null, params object[] metadata) =>
        new(_ => Task.CompletedTask,
            RoutePatternFactory.Parse(pattern, defaults, parameterPolicies: null, requiredValues),
            0,
            new EndpointMetadataCollection(metadata),
            pattern);

    private static (LegacyRouteOwnership Service, HttpContext Context) Build(params RouteEndpoint[] endpoints)
    {
        var services = new ServiceCollection().AddRouting().BuildServiceProvider();
        var resolver = services.GetRequiredService<IInlineConstraintResolver>();
        var context = new DefaultHttpContext { RequestServices = services };
        return (new LegacyRouteOwnership(new DefaultEndpointDataSource(endpoints), resolver), context);
    }

    [Fact]
    public void AnMvcActionOwnsItsPath()
    {
        var (service, context) = Build(
            Endpoint("Error/{status}"),
            // An attribute route carries its action as defaults and required values alike.
            Endpoint("Users/Login", defaults: new { controller = "Account", action = "Login" }, requiredValues: new { controller = "Account", action = "Login" }));

        Assert.True(service.Owns(context, "/Error/404"));
        Assert.True(service.Owns(context, "/users/login"));
        Assert.False(service.Owns(context, "/about"));
        Assert.False(service.Owns(context, "/Error"));
    }

    [Fact]
    public void AConventionalRouteOwnsOnlyItsOwnAction()
    {
        // Endpoint routing expands a conventional route into one endpoint per action, told
        // apart by required values; the template alone would match any three segments.
        var (service, context) = Build(
            Endpoint("{area:exists}/{controller=Home}/{action=Index}/{id?}",
                requiredValues: new { area = "Crest.Email", controller = "Admin", action = "Test" }));

        Assert.True(service.Owns(context, "/Crest.Email/Admin/Test"));
        Assert.False(service.Owns(context, "/about/us/now"));
        Assert.False(service.Owns(context, "/about"));
    }

    [Fact]
    public void InlineConstraintsApply()
    {
        var (service, context) = Build(Endpoint("Error/{status:int}"));

        Assert.True(service.Owns(context, "/Error/500"));
        Assert.False(service.Owns(context, "/Error/oops"));
    }

    [Fact]
    public void BlazorPagesAndTheStaticAssetsFallbackAreNotLegacy()
    {
        var (service, context) = Build(
            Endpoint("{**path:file}"),
            Endpoint("shop/{id}", metadata: new Microsoft.AspNetCore.Components.Endpoints.ComponentTypeMetadata(typeof(object))),
            Endpoint("members/{*rest}", metadata: new ThemeOwnerMetadata(ShellId.Member)));

        Assert.False(service.Owns(context, "/site.css"));
        Assert.False(service.Owns(context, "/shop/1"));
        Assert.False(service.Owns(context, "/members/acme"));
    }
}

// The shipped account email templates are registered under the names UserEmailService renders.
public sealed class AccountEmailTemplatesTests
{
    [Fact]
    public async Task BothAccountEmailsShipUnderTheirNames()
    {
        var templates = await new Crest.Display.AccountEmailTemplates().GetTemplatesAsync();

        Assert.Contains(templates, template => template.Name == Crest.Users.Services.UserEmailService.ConfirmEmailTemplate && template.Published);
        Assert.Contains(templates, template => template.Name == Crest.Users.Services.UserEmailService.LostPasswordTemplate && template.Published);
    }
}
