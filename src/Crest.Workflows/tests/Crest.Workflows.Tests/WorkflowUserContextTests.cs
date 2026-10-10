using System.Security.Claims;
using Crest.Access;
using Crest.Workflows.Contexts;
using Crest.Workflows.Management;
using Crest.Security.Permissions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Crest.Workflows.Tests;

// The actor is identity only (docs/operations.md › Caller lifetime): what a trigger captures
// carries who acted and in which shell, never rights. The caller is rebuilt from that
// identity through the access factory at every burst, and the one decision answers.
public class WorkflowUserContextTests
{
    private static WorkflowCallerResolver Resolver(ICallerContextFactory factory, CallerContext? current = null)
    {
        var accessor = Substitute.For<ICallerContextAccessor>();
        accessor.Current.Returns(current);
        return new WorkflowCallerResolver(factory, accessor, Substitute.For<IHttpContextAccessor>(), Substitute.For<IWorkflowInstanceStore>(), Substitute.For<IWorkflowDefinitionService>());
    }

    [Fact]
    public void A_principal_captures_identity_and_shell_only()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "alice"),
            new Claim(ClaimTypes.NameIdentifier, "u-1"),
            new Claim(ClaimTypes.Role, "Editor"),
            new Claim("crest_class", "member"),
        ], "Identity.Application"));

        var actor = WorkflowUserContext.From(principal, "acme", CallerSide.Admin);

        Assert.True(actor.IsAuthenticated);
        Assert.Equal("u-1", actor.UserId);
        Assert.Equal("alice", actor.UserName);
        Assert.Equal("acme", actor.Tenant);
        Assert.Equal(CallerSide.Admin, actor.Side);
        Assert.False(actor.IsSystem);
        Assert.Null(actor.OrganizationId);

        // What goes to the factory is the identity, nothing a role or a class could ride on.
        var identity = actor.ToIdentityPrincipal();
        Assert.Equal(["u-1", "alice"], identity.Claims.Select(c => c.Value));
        Assert.DoesNotContain(identity.Claims, c => c.Type == ClaimTypes.Role || c.Type == "crest_class");
    }

    [Fact]
    public void A_built_caller_captures_its_identity_side_organization_and_system_flag()
    {
        var caller = new CallerContext
        {
            Tenant = "acme",
            Side = CallerSide.Member,
            UserId = "u-2",
            UserName = "bob",
            OrganizationId = "org-9",
            Roles = new HashSet<string> { "Member" },
            Permissions = new HashSet<string> { "ViewContent" },
            IsSuperUser = true,
        };

        var actor = WorkflowUserContext.From(caller);

        Assert.Equal(("u-2", "bob", "acme", CallerSide.Member, "org-9", false), (actor.UserId, actor.UserName, actor.Tenant, actor.Side, actor.OrganizationId, actor.IsSystem));
        Assert.True(WorkflowUserContext.From(new CallerContext { Tenant = "acme", Side = CallerSide.System, IsSystem = true }).IsSystem);
        Assert.False(WorkflowUserContext.Anonymous("acme").IsAuthenticated);
        Assert.False(WorkflowUserContext.From(new ClaimsPrincipal(new ClaimsIdentity()), "acme").IsAuthenticated);
    }

    [Fact]
    public void A_persisted_actor_reads_back_from_json()
    {
        var json = System.Text.Json.JsonSerializer.SerializeToElement(new { userId = "u-1", userName = "alice", tenant = "acme", side = "Member", organizationId = "org-9", isSystem = false });

        var actor = WorkflowCallerResolver.Actor(json)!;

        Assert.Equal("u-1", actor.UserId);
        Assert.Equal(CallerSide.Member, actor.Side);
        Assert.Equal("org-9", actor.OrganizationId);
        Assert.Null(WorkflowCallerResolver.Actor(null));
        Assert.Null(WorkflowCallerResolver.Actor("not an actor"));
    }

    [Fact]
    public async Task The_actor_is_rebuilt_into_a_caller_with_current_rights_and_a_system_actor_is_refused()
    {
        var factory = Substitute.For<ICallerContextFactory>();
        factory.CreateAsync(Arg.Any<CallerRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<CallerRequest>()!;
                return Task.FromResult(new CallerContext { Tenant = "acme", Side = request.Side, UserId = request.Principal!.FindFirstValue(ClaimTypes.NameIdentifier), OrganizationId = request.RequestedOrganizationId });
            });
        var resolver = Resolver(factory);

        var caller = await resolver.FromActorAsync(new WorkflowUserContext { UserId = "u-1", UserName = "alice", Tenant = "acme", Side = CallerSide.Member, OrganizationId = "org-9" }, TestContext.Current.CancellationToken);

        Assert.Equal("u-1", caller.UserId);
        Assert.Equal(CallerSide.Member, caller.Side);
        Assert.Equal("org-9", caller.OrganizationId);
        await Assert.ThrowsAsync<WorkflowAccessRefusedException>(() => resolver.FromActorAsync(new WorkflowUserContext { UserName = "system", Tenant = "acme", Side = CallerSide.System, IsSystem = true }, TestContext.Current.CancellationToken));
        await factory.DidNotReceive().CreateSystemAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorizer_denies_anonymous_blank_and_unknown_permissions_without_asking_the_decision()
    {
        var decision = Substitute.For<IAccessDecision>();
        var permissions = Substitute.For<IPermissionService>();
        permissions.FindByNameAsync("Nope").Returns(new ValueTask<Permission>((Permission)null!));
        var authorizer = new WorkflowAuthorizer(Resolver(Substitute.For<ICallerContextFactory>()), decision, permissions);
        var user = new WorkflowUserContext { UserId = "u-1", UserName = "alice", Tenant = "acme" };

        Assert.False(await authorizer.AuthorizeAsync(null, "ManageWorkflows", TestContext.Current.CancellationToken));
        Assert.False(await authorizer.AuthorizeAsync(WorkflowUserContext.Anonymous("acme"), "ManageWorkflows", TestContext.Current.CancellationToken));
        Assert.False(await authorizer.AuthorizeAsync(user, " ", TestContext.Current.CancellationToken));
        Assert.False(await authorizer.AuthorizeAsync(user, "Nope", TestContext.Current.CancellationToken));
        await decision.DidNotReceive().DecideAsync(Arg.Any<CallerContext>(), Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorizer_asks_the_decision_for_the_rebuilt_caller()
    {
        var permission = new Permission("PublishContent", "Publish content");
        var permissions = Substitute.For<IPermissionService>();
        permissions.FindByNameAsync("PublishContent").Returns(new ValueTask<Permission>(permission));
        var factory = Substitute.For<ICallerContextFactory>();
        factory.CreateAsync(Arg.Any<CallerRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new CallerContext { Tenant = "acme", Side = CallerSide.Admin, UserId = call.Arg<CallerRequest>()!.Principal!.FindFirstValue(ClaimTypes.NameIdentifier) }));
        var decision = Substitute.For<IAccessDecision>();
        decision.DecideAsync(Arg.Any<CallerContext>(), "PublishContent", Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<CallerContext>()!.UserId == "u-1" ? AccessDecision.Allowed : AccessDecision.Denied("not alice")));
        var authorizer = new WorkflowAuthorizer(Resolver(factory), decision, permissions);

        Assert.True(await authorizer.AuthorizeAsync(new WorkflowUserContext { UserId = "u-1", UserName = "alice", Tenant = "acme" }, "PublishContent", TestContext.Current.CancellationToken));
        Assert.False(await authorizer.AuthorizeAsync(new WorkflowUserContext { UserId = "u-2", UserName = "bob", Tenant = "acme" }, "PublishContent", TestContext.Current.CancellationToken));
    }
}
