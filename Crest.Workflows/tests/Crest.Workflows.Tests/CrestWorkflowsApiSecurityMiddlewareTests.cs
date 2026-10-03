using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Crest.Workflows.Security;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using Xunit;

namespace Crest.Workflows.Tests;

// The API gate, decision by decision. The middleware is the only thing between a tenant
// cookie and the engine's endpoints, so each branch is pinned: path scope, 401, 403 (through
// IAuthorizationService, i.e. every registered handler), antiforgery on writes, the
// per-request grant mapped permission by permission onto the engine's names, the per-
// definition Run check, and the 403 a refused change surfaces as.
public class CrestWorkflowsApiSecurityMiddlewareTests
{
    private static ClaimsPrincipal User(bool authenticated) => authenticated
        ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "Test"))
        : new ClaimsPrincipal(new ClaimsIdentity());

    private static readonly IWorkflowDefinitionAccessReader NoLists = Substitute.For<IWorkflowDefinitionAccessReader>();
    // No endpoint is matched on a bare DefaultHttpContext, so the provider is never asked.
    private static readonly IAuthorizationPolicyProvider Policies = Substitute.For<IAuthorizationPolicyProvider>();

    private static (CrestWorkflowsApiSecurityMiddleware Middleware, DefaultHttpContext Context, IAuthorizationService Authorization, IAntiforgery Antiforgery, List<bool> Reached)
        Build(string path, string method, bool authenticated, bool permitted, bool antiforgeryValid, Func<HttpContext, Task>? endpoint = null, params string[] onlyPermissions)
    {
        var reached = new List<bool>();
        var middleware = new CrestWorkflowsApiSecurityMiddleware(async context => { reached.Add(true); if (endpoint is not null) await endpoint(context); }, NullLogger<CrestWorkflowsApiSecurityMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.User = User(authenticated);

        // permitted: every workflow permission; onlyPermissions narrows it to the named ones.
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(call =>
            {
                var names = (call.Arg<IEnumerable<IAuthorizationRequirement>>() ?? []).OfType<PermissionRequirement>().Select(r => r.Permission.Name).ToList();
                var granted = permitted && (onlyPermissions.Length == 0 || names.All(onlyPermissions.Contains));
                return granted ? AuthorizationResult.Success() : AuthorizationResult.Failed();
            });

        var antiforgery = Substitute.For<IAntiforgery>();
        antiforgery.IsRequestValidAsync(Arg.Any<HttpContext>()).Returns(antiforgeryValid);

        return (middleware, context, authorization, antiforgery, reached);
    }

    private static bool IsRunResource(object? resource, string definitionId, string runner) =>
        resource is WorkflowDefinitionAccessResource r && r.DefinitionId == definitionId && r.Run.Contains(runner);

    private static IEnumerable<string> Grants(HttpContext context) => context.User.Claims.Where(c => c.Type == CrestWorkflowsApiSecurityMiddleware.CrestWorkflowsPermissionsClaimType).Select(c => c.Value);

    [Fact]
    public async Task Requests_outside_the_api_path_pass_through_untouched()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/api/crest/parties", "POST", authenticated: false, permitted: false, antiforgeryValid: false);

        await middleware.InvokeAsync(context, authorization, Policies, antiforgery, NoLists);

        Assert.Single(reached);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.DoesNotContain(context.User.Claims, claim => claim.Type == CrestWorkflowsApiSecurityMiddleware.CrestWorkflowsPermissionsClaimType);
    }

    [Fact]
    public async Task Anonymous_gets_401()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/crest-workflows/api/workflow-definitions", "GET", authenticated: false, permitted: true, antiforgeryValid: true);

        await middleware.InvokeAsync(context, authorization, Policies, antiforgery, NoLists);

        Assert.Empty(reached);
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_without_ViewWorkflows_gets_403()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/crest-workflows/api/workflow-definitions", "GET", authenticated: true, permitted: false, antiforgeryValid: true);

        await middleware.InvokeAsync(context, authorization, Policies, antiforgery, NoLists);

        Assert.Empty(reached);
        Assert.Equal(403, context.Response.StatusCode);
        await authorization.Received(1).AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<object?>(),
            Arg.Is<IEnumerable<IAuthorizationRequirement>>(requirements => requirements!.OfType<PermissionRequirement>().Any(requirement => requirement.Permission.Name == WorkflowsConstants.Permissions.View)));
    }

    [Fact]
    public async Task Write_without_antiforgery_gets_400_even_when_permitted()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/crest-workflows/api/workflow-definitions", "POST", authenticated: true, permitted: true, antiforgeryValid: false);

        await middleware.InvokeAsync(context, authorization, Policies, antiforgery, NoLists);

        Assert.Empty(reached);
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task Read_by_a_permitted_user_needs_no_antiforgery_and_gets_the_engine_grant()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/crest-workflows/api/workflow-definitions", "GET", authenticated: true, permitted: true, antiforgeryValid: false);

        await middleware.InvokeAsync(context, authorization, Policies, antiforgery, NoLists);

        Assert.Single(reached);
        var grants = Grants(context).ToList();
        Assert.Contains("read:workflow-definitions", grants);
        Assert.Contains("write:workflow-definitions", grants);
        Assert.Contains("publish:workflow-definitions", grants);
        Assert.Contains("exec:workflow-definitions", grants);
        Assert.DoesNotContain(CrestWorkflowsApiSecurityMiddleware.CrestWorkflowsAllPermissions, grants);
        await antiforgery.DidNotReceive().IsRequestValidAsync(Arg.Any<HttpContext>());
    }

    [Fact]
    public async Task A_view_only_user_gets_read_grants_and_nothing_else()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/crest-workflows/api/workflow-definitions", "GET", authenticated: true, permitted: true, antiforgeryValid: false, null, WorkflowsConstants.Permissions.View);

        await middleware.InvokeAsync(context, authorization, Policies, antiforgery, NoLists);

        Assert.Single(reached);
        var grants = Grants(context).ToList();
        Assert.Contains("read:workflow-definitions", grants);
        Assert.Contains("read:workflow-instances", grants);
        Assert.DoesNotContain("write:workflow-definitions", grants);
        Assert.DoesNotContain("publish:workflow-definitions", grants);
        Assert.DoesNotContain("exec:workflow-definitions", grants);
        Assert.DoesNotContain("delete:workflow-instances", grants);
    }

    [Fact]
    public async Task Starting_a_definition_is_authorized_against_that_definition()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/crest-workflows/api/workflow-definitions/def-1/execute", "POST", authenticated: true, permitted: true, antiforgeryValid: true);
        var lists = Substitute.For<IWorkflowDefinitionAccessReader>();
        lists.GetAsync("def-1").Returns(new WorkflowDefinitionAccessResource("def-1", [], ["Operators"]));

        await middleware.InvokeAsync(context, authorization, Policies, antiforgery, lists);

        Assert.Single(reached);
        await authorization.Received().AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Is<object?>(resource => IsRunResource(resource, "def-1", "Operators")),
            Arg.Is<IEnumerable<IAuthorizationRequirement>>(requirements => requirements!.OfType<PermissionRequirement>().Any(requirement => requirement.Permission.Name == WorkflowsConstants.Permissions.Run)));
    }

    [Fact]
    public async Task Starting_a_definition_without_Run_gets_403_before_the_engine()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/crest-workflows/api/workflow-definitions/def-1/dispatch", "POST", authenticated: true, permitted: true, antiforgeryValid: true, null, WorkflowsConstants.Permissions.View, WorkflowsConstants.Permissions.Edit);

        await middleware.InvokeAsync(context, authorization, Policies, antiforgery, NoLists);

        Assert.Empty(reached);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task A_change_the_ownership_tier_refuses_surfaces_as_403_with_the_reason()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/crest-workflows/api/workflow-definitions/def-1/publish", "POST", authenticated: true, permitted: true, antiforgeryValid: true,
            _ => throw new Registry.WorkflowChangeDeniedException("system workflow"));
        var body = new MemoryStream();
        context.Response.Body = body;

        await middleware.InvokeAsync(context, authorization, Policies, antiforgery, NoLists);

        Assert.Single(reached);
        Assert.Equal(403, context.Response.StatusCode);
        Assert.Contains("system workflow", System.Text.Encoding.UTF8.GetString(body.ToArray()));
    }

    [Fact]
    public async Task Write_by_a_permitted_user_with_a_valid_token_proceeds()
    {
        var (middleware, context, authorization, antiforgery, reached) = Build("/crest-workflows/api/workflow-definitions/abc/publish", "POST", authenticated: true, permitted: true, antiforgeryValid: true);

        await middleware.InvokeAsync(context, authorization, Policies, antiforgery, NoLists);

        Assert.Single(reached);
        Assert.Equal(200, context.Response.StatusCode);
    }
}
