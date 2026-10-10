using System.Security.Claims;
using Crest.Access;
using Crest.Workflows.Contexts;
using Crest.Workflows.Management;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Crest.Workflows.Security;
using Crest.Security;
using Xunit;

namespace Crest.Workflows.Tests;

// The API gate, decision by decision. The middleware is the only thing between a tenant
// cookie and the engine's endpoints, so each branch is pinned: path scope, 401, 403 (through
// the one access decision for the request's caller), antiforgery on writes, the per-request
// grant mapped permission by permission onto the engine's names, the per-definition Run
// check, and the 403 a refused change surfaces as.
public class CrestWorkflowsApiSecurityMiddlewareTests
{
    private static ClaimsPrincipal User(bool authenticated) => authenticated
        ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "Test"))
        : new ClaimsPrincipal(new ClaimsIdentity());

    private static readonly IWorkflowDefinitionAccessReader NoLists = Substitute.For<IWorkflowDefinitionAccessReader>();
    // No endpoint is matched on a bare DefaultHttpContext, so the provider is never asked.
    private static readonly IAuthorizationPolicyProvider Policies = Substitute.For<IAuthorizationPolicyProvider>();

    private static readonly CallerContext Alice = new() { Tenant = "acme", Side = CallerSide.Admin, UserId = "u-1", UserName = "alice" };

    private sealed record Gate(CrestWorkflowsApiSecurityMiddleware Middleware, DefaultHttpContext Context, IAccessDecision Decision, IAntiforgery Antiforgery, List<bool> Reached)
    {
        // The caller is the gated one (set by the request path), as in production.
        public Task InvokeAsync(IWorkflowDefinitionAccessReader lists)
        {
            var accessor = Substitute.For<ICallerContextAccessor>();
            accessor.Current.Returns(Context.User.Identity?.IsAuthenticated == true ? Alice : null);
            var callers = new WorkflowCallerResolver(Substitute.For<ICallerContextFactory>(), accessor, Substitute.For<IHttpContextAccessor>(), Substitute.For<IWorkflowInstanceStore>(), Substitute.For<IWorkflowDefinitionService>());
            return Middleware.InvokeAsync(Context, callers, Decision, Substitute.For<IAuthorizationService>(), Policies, Antiforgery, lists);
        }
    }

    private static Gate Build(string path, string method, bool authenticated, bool permitted, bool antiforgeryValid, Func<HttpContext, Task>? endpoint = null, params string[] onlyPermissions)
    {
        var reached = new List<bool>();
        var middleware = new CrestWorkflowsApiSecurityMiddleware(async context => { reached.Add(true); if (endpoint is not null) await endpoint(context); }, NullLogger<CrestWorkflowsApiSecurityMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.User = User(authenticated);

        // permitted: every workflow permission; onlyPermissions narrows it to the named ones.
        var decision = Substitute.For<IAccessDecision>();
        decision.DecideAsync(Arg.Any<CallerContext>(), Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var granted = permitted && (onlyPermissions.Length == 0 || onlyPermissions.Contains(call.Arg<string>()));
                return Task.FromResult(granted ? AccessDecision.Allowed : AccessDecision.Denied("no"));
            });

        var antiforgery = Substitute.For<IAntiforgery>();
        antiforgery.IsRequestValidAsync(Arg.Any<HttpContext>()).Returns(antiforgeryValid);

        return new(middleware, context, decision, antiforgery, reached);
    }

    private static bool IsRunResource(object? resource, string definitionId, string runner) =>
        resource is WorkflowDefinitionAccessResource r && r.DefinitionId == definitionId && r.Run.Contains(runner);

    private static IEnumerable<string> Grants(HttpContext context) => context.User.Claims.Where(c => c.Type == CrestWorkflowsApiSecurityMiddleware.CrestWorkflowsPermissionsClaimType).Select(c => c.Value);

    [Fact]
    public async Task Requests_outside_the_api_path_pass_through_untouched()
    {
        var gate = Build("/api/crest/parties", "POST", authenticated: false, permitted: false, antiforgeryValid: false);

        await gate.InvokeAsync(NoLists);

        Assert.Single(gate.Reached);
        Assert.Equal(200, gate.Context.Response.StatusCode);
        Assert.DoesNotContain(gate.Context.User.Claims, claim => claim.Type == CrestWorkflowsApiSecurityMiddleware.CrestWorkflowsPermissionsClaimType);
    }

    [Fact]
    public async Task Anonymous_gets_401()
    {
        var gate = Build("/crest-workflows/api/workflow-definitions", "GET", authenticated: false, permitted: true, antiforgeryValid: true);

        await gate.InvokeAsync(NoLists);

        Assert.Empty(gate.Reached);
        Assert.Equal(401, gate.Context.Response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_without_ViewWorkflows_gets_403()
    {
        var gate = Build("/crest-workflows/api/workflow-definitions", "GET", authenticated: true, permitted: false, antiforgeryValid: true);

        await gate.InvokeAsync(NoLists);

        Assert.Empty(gate.Reached);
        Assert.Equal(403, gate.Context.Response.StatusCode);
        await gate.Decision.Received(1).DecideAsync(Alice, WorkflowsConstants.Permissions.View, Arg.Any<object?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Write_without_antiforgery_gets_400_even_when_permitted()
    {
        var gate = Build("/crest-workflows/api/workflow-definitions", "POST", authenticated: true, permitted: true, antiforgeryValid: false);

        await gate.InvokeAsync(NoLists);

        Assert.Empty(gate.Reached);
        Assert.Equal(400, gate.Context.Response.StatusCode);
    }

    [Fact]
    public async Task Read_by_a_permitted_user_needs_no_antiforgery_and_gets_the_engine_grant()
    {
        var gate = Build("/crest-workflows/api/workflow-definitions", "GET", authenticated: true, permitted: true, antiforgeryValid: false);

        await gate.InvokeAsync(NoLists);

        Assert.Single(gate.Reached);
        var grants = Grants(gate.Context).ToList();
        Assert.Contains("read:workflow-definitions", grants);
        Assert.Contains("write:workflow-definitions", grants);
        Assert.Contains("publish:workflow-definitions", grants);
        Assert.Contains("exec:workflow-definitions", grants);
        Assert.DoesNotContain(CrestWorkflowsApiSecurityMiddleware.CrestWorkflowsAllPermissions, grants);
        await gate.Antiforgery.DidNotReceive().IsRequestValidAsync(Arg.Any<HttpContext>());
    }

    [Fact]
    public async Task A_view_only_user_gets_read_grants_and_nothing_else()
    {
        var gate = Build("/crest-workflows/api/workflow-definitions", "GET", authenticated: true, permitted: true, antiforgeryValid: false, null, WorkflowsConstants.Permissions.View);

        await gate.InvokeAsync(NoLists);

        Assert.Single(gate.Reached);
        var grants = Grants(gate.Context).ToList();
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
        var gate = Build("/crest-workflows/api/workflow-definitions/def-1/execute", "POST", authenticated: true, permitted: true, antiforgeryValid: true);
        var lists = Substitute.For<IWorkflowDefinitionAccessReader>();
        lists.GetAsync("def-1").Returns(new WorkflowDefinitionAccessResource("def-1", [], ["Operators", "alice"]));

        await gate.InvokeAsync(lists);

        Assert.Single(gate.Reached);
        await gate.Decision.Received().DecideAsync(Alice, WorkflowsConstants.Permissions.Run, Arg.Is<object?>(resource => IsRunResource(resource, "def-1", "Operators")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Starting_a_definition_whose_Run_list_excludes_the_caller_gets_403_despite_the_permission()
    {
        var gate = Build("/crest-workflows/api/workflow-definitions/def-1/execute", "POST", authenticated: true, permitted: true, antiforgeryValid: true);
        var lists = Substitute.For<IWorkflowDefinitionAccessReader>();
        lists.GetAsync("def-1").Returns(new WorkflowDefinitionAccessResource("def-1", [], ["Operators"]));

        await gate.InvokeAsync(lists);

        Assert.Empty(gate.Reached);
        Assert.Equal(403, gate.Context.Response.StatusCode);
    }

    [Fact]
    public async Task Starting_a_definition_without_Run_gets_403_before_the_engine()
    {
        var gate = Build("/crest-workflows/api/workflow-definitions/def-1/dispatch", "POST", authenticated: true, permitted: true, antiforgeryValid: true, null, WorkflowsConstants.Permissions.View, WorkflowsConstants.Permissions.Edit);

        await gate.InvokeAsync(NoLists);

        Assert.Empty(gate.Reached);
        Assert.Equal(403, gate.Context.Response.StatusCode);
    }

    [Fact]
    public async Task A_change_the_ownership_tier_refuses_surfaces_as_403_with_the_reason()
    {
        var gate = Build("/crest-workflows/api/workflow-definitions/def-1/publish", "POST", authenticated: true, permitted: true, antiforgeryValid: true,
            _ => throw new Registry.WorkflowChangeDeniedException("system workflow"));
        var body = new MemoryStream();
        gate.Context.Response.Body = body;

        await gate.InvokeAsync(NoLists);

        Assert.Single(gate.Reached);
        Assert.Equal(403, gate.Context.Response.StatusCode);
        Assert.Contains("system workflow", System.Text.Encoding.UTF8.GetString(body.ToArray()));
    }

    [Fact]
    public async Task Write_by_a_permitted_user_with_a_valid_token_proceeds()
    {
        var gate = Build("/crest-workflows/api/workflow-definitions/abc/publish", "POST", authenticated: true, permitted: true, antiforgeryValid: true);

        await gate.InvokeAsync(NoLists);

        Assert.Single(gate.Reached);
        Assert.Equal(200, gate.Context.Response.StatusCode);
    }
}
