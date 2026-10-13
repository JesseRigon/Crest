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
        return new WorkflowCallerResolver(factory, accessor, AccessGateTests.Seal, Substitute.For<IWorkflowInstanceStore>(), Substitute.For<IWorkflowDefinitionService>());
    }

    // A machine's run is attributed to its client id (docs/machine-actors.md); a user's is not.
    [Fact]
    public void An_application_caller_carries_its_client_id()
    {
        var machine = new CallerContext { Tenant = "acme", Side = CallerSide.Admin, UserId = "ledger-sync", UserName = "Ledger sync", UserClass = CallerClasses.Application };
        var person = new CallerContext { Tenant = "acme", Side = CallerSide.Admin, UserId = "u-1", UserName = "alice" };

        Assert.Equal("ledger-sync", WorkflowUserContext.From(machine).ClientId);
        Assert.Null(WorkflowUserContext.From(person).ClientId);
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
    public void A_sealed_actor_reads_back_and_anything_else_reads_as_none()
    {
        var actor = new WorkflowUserContext { UserId = "u-1", UserName = "alice", Tenant = "acme", Side = CallerSide.Member, OrganizationId = "org-9" };

        var opened = AccessGateTests.Seal.Open(AccessGateTests.Seal.Seal(actor), instanceId: null, acceptFresh: true)!;

        Assert.Equal("u-1", opened.UserId);
        Assert.Equal(CallerSide.Member, opened.Side);
        Assert.Equal("org-9", opened.OrganizationId);
        Assert.Null(AccessGateTests.Seal.Open(null, null, acceptFresh: true));
        Assert.Null(AccessGateTests.Seal.Open("not an actor", null, acceptFresh: true));
        Assert.Null(AccessGateTests.Seal.Open(System.Text.Json.JsonSerializer.SerializeToElement(actor), null, acceptFresh: true));
        Assert.Null(AccessGateTests.Seal.Open(actor, null, acceptFresh: true));
    }

    [Fact]
    public void A_fresh_actor_opens_only_on_a_first_burst()
    {
        var sealedActor = AccessGateTests.Seal.Seal(new WorkflowUserContext { UserId = "u-1", Tenant = "acme" });

        Assert.NotNull(AccessGateTests.Seal.Open(sealedActor, "i-1", acceptFresh: true));
        Assert.Null(AccessGateTests.Seal.Open(sealedActor, "i-1", acceptFresh: false));
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

    private sealed class Accessor(CallerContext? current) : ICallerContextAccessor
    {
        public CallerContext? Current { get; set; } = current;
    }

    [Fact]
    public async Task Authorizer_denies_no_caller_anonymous_blank_and_unknown_permissions_without_asking_the_decision()
    {
        var decision = Substitute.For<IAccessDecision>();
        var permissions = Substitute.For<IPermissionService>();
        permissions.FindByNameAsync("Nope").Returns(new ValueTask<Permission>((Permission)null!));
        var alice = new CallerContext { Tenant = "acme", Side = CallerSide.Admin, UserId = "u-1", UserName = "alice" };

        Assert.False(await new WorkflowAuthorizer(new Accessor(null), decision, permissions).AuthorizeAsync("ManageWorkflows", TestContext.Current.CancellationToken));
        Assert.False(await new WorkflowAuthorizer(new Accessor(CallerContext.Anonymous("acme", CallerSide.Site)), decision, permissions).AuthorizeAsync("ManageWorkflows", TestContext.Current.CancellationToken));
        Assert.False(await new WorkflowAuthorizer(new Accessor(alice), decision, permissions).AuthorizeAsync(" ", TestContext.Current.CancellationToken));
        Assert.False(await new WorkflowAuthorizer(new Accessor(alice), decision, permissions).AuthorizeAsync("Nope", TestContext.Current.CancellationToken));
        await decision.DidNotReceive().DecideAsync(Arg.Any<CallerContext>(), Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorizer_asks_the_decision_for_the_bursts_caller()
    {
        var permission = new Permission("PublishContent", "Publish content");
        var permissions = Substitute.For<IPermissionService>();
        permissions.FindByNameAsync("PublishContent").Returns(new ValueTask<Permission>(permission));
        var decision = Substitute.For<IAccessDecision>();
        decision.DecideAsync(Arg.Any<CallerContext>(), "PublishContent", Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<CallerContext>()!.UserId == "u-1" ? AccessDecision.Allowed : AccessDecision.Denied("not alice")));

        Assert.True(await new WorkflowAuthorizer(new Accessor(new CallerContext { Tenant = "acme", Side = CallerSide.Admin, UserId = "u-1" }), decision, permissions).AuthorizeAsync("PublishContent", TestContext.Current.CancellationToken));
        Assert.False(await new WorkflowAuthorizer(new Accessor(new CallerContext { Tenant = "acme", Side = CallerSide.Admin, UserId = "u-2" }), decision, permissions).AuthorizeAsync("PublishContent", TestContext.Current.CancellationToken));
    }
}
