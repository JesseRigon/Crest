using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OrchardCore.Crest.Workflows.Security;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using Xunit;

namespace OrchardCore.Crest.Workflows.Tests;

// The API gate, decision by decision. The middleware is the only thing between a tenant
// cookie and Elsa's endpoints, so each branch is pinned: path scope, 401, 403 (through
// IAuthorizationService, i.e. every registered handler), antiforgery on writes, and the
// per-request grant that Elsa reads.
public class ElsaApiSecurityMiddlewareTests
{
    private static ClaimsPrincipal User(bool authenticated) => authenticated
        ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "Test"))
        : new ClaimsPrincipal(new ClaimsIdentity());

    private static (ElsaApiSecurityMiddleware Middleware, DefaultHttpContext Context, IAuthorizationService Authorization, IAntiforgery Antiforgery, List<bool> Reached)
        Build(string path, string method, bool authenticated, bool permitted, bool antiforgeryValid)
    {
        var reached = new List<bool>();
        var middleware = new ElsaApiSecurityMiddleware(_ => { reached.Add(true); return Task.CompletedTask; }, NullLogger<ElsaApiSecurityMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.User = User(authenticated);

        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(permitted ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var antiforgery = Substitute.For<IAntiforgery>();
        antiforgery.IsRequestValidAsync(Arg.Any<HttpContext>()).Returns(antiforgeryValid);

        return (middleware, context, authorization, antiforgery, reached);
    }

    [Fact]
    public async Task Requests_outside_the_api_path_pass_through_untouched()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/api/crest/parties", "POST", authenticated: false, permitted: false, antiforgeryValid: false);

        await middleware.InvokeAsync(context, authorization, antiforgery);

        Assert.Single(reached);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.DoesNotContain(context.User.Claims, claim => claim.Type == ElsaApiSecurityMiddleware.ElsaPermissionsClaimType);
    }

    [Fact]
    public async Task Anonymous_gets_401()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/elsa/api/workflow-definitions", "GET", authenticated: false, permitted: true, antiforgeryValid: true);

        await middleware.InvokeAsync(context, authorization, antiforgery);

        Assert.Empty(reached);
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_without_ManageWorkflows_gets_403()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/elsa/api/workflow-definitions", "GET", authenticated: true, permitted: false, antiforgeryValid: true);

        await middleware.InvokeAsync(context, authorization, antiforgery);

        Assert.Empty(reached);
        Assert.Equal(403, context.Response.StatusCode);
        await authorization.Received(1).AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<object?>(),
            Arg.Is<IEnumerable<IAuthorizationRequirement>>(requirements => requirements!.OfType<PermissionRequirement>().Any(requirement => requirement.Permission.Name == "ManageWorkflows")));
    }

    [Fact]
    public async Task Write_without_antiforgery_gets_400_even_when_permitted()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/elsa/api/workflow-definitions", "POST", authenticated: true, permitted: true, antiforgeryValid: false);

        await middleware.InvokeAsync(context, authorization, antiforgery);

        Assert.Empty(reached);
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task Read_by_a_permitted_user_needs_no_antiforgery_and_gets_the_Elsa_grant()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/elsa/api/workflow-definitions", "GET", authenticated: true, permitted: true, antiforgeryValid: false);

        await middleware.InvokeAsync(context, authorization, antiforgery);

        Assert.Single(reached);
        var grant = Assert.Single(context.User.Claims, claim => claim.Type == ElsaApiSecurityMiddleware.ElsaPermissionsClaimType);
        Assert.Equal(ElsaApiSecurityMiddleware.ElsaAllPermissions, grant.Value);
        await antiforgery.DidNotReceive().IsRequestValidAsync(Arg.Any<HttpContext>());
    }

    [Fact]
    public async Task Write_by_a_permitted_user_with_a_valid_token_proceeds()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/elsa/api/workflow-definitions/abc/publish", "POST", authenticated: true, permitted: true, antiforgeryValid: true);

        await middleware.InvokeAsync(context, authorization, antiforgery);

        Assert.Single(reached);
        Assert.Equal(200, context.Response.StatusCode);
    }
}
