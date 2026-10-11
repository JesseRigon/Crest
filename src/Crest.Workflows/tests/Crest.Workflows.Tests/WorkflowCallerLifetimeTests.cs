using System.Security.Claims;
using Crest.Access;
using Crest.Workflows.Activities;
using Crest.Workflows.Contexts;
using Crest.Workflows.Management;
using Crest.Workflows.Models;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Crest.Workflows.Tests;

// Caller lifetime for workflow bursts (docs/workflows.md › Caller lifetime): the caller is
// rebuilt from the identity-only actor at every burst, so a right revoked between two bursts
// of one run is gone on the second; nothing about rights survives in the instance input.
public class WorkflowCallerLifetimeTests
{
    private sealed class Rights
    {
        public HashSet<string> Granted { get; } = new(StringComparer.OrdinalIgnoreCase) { "EditContent", "PublishContent" };

        public long Version { get; set; } = 1;

        public int Builds { get; set; }
    }

    private static ICallerContextFactory Factory(Rights rights)
    {
        var factory = Substitute.For<ICallerContextFactory>();
        factory.CreateAsync(Arg.Any<CallerRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                rights.Builds++;
                var request = call.Arg<CallerRequest>()!;
                return Task.FromResult(new CallerContext
                {
                    Tenant = "acme",
                    Side = request.Side,
                    UserId = request.Principal!.FindFirstValue(ClaimTypes.NameIdentifier),
                    OrganizationId = request.RequestedOrganizationId,
                    Permissions = new HashSet<string>(rights.Granted, StringComparer.OrdinalIgnoreCase),
                    PermissionVersion = rights.Version,
                });
            });
        return factory;
    }

    private static WorkflowCallerResolver Resolver(ICallerContextFactory factory) =>
        new(factory, Substitute.For<ICallerContextAccessor>(), AccessGateTests.Seal, Substitute.For<IWorkflowInstanceStore>(), Substitute.For<IWorkflowDefinitionService>());

    private static Workflow Definition(bool runsAsSystem = false)
    {
        var workflow = new Workflow { CustomProperties = runsAsSystem ? new Dictionary<string, object> { [WorkflowsConstants.RunsAsSystemProperty] = true } : new Dictionary<string, object>() };
        workflow.Id = "wf";
        workflow.WorkflowMetadata = new WorkflowMetadata("Approval");
        return workflow;
    }

    private const string Instance = "instance-1";

    // The run's actor as the gate leaves it after the first burst: bound to the instance.
    private static Dictionary<string, object> InputFor(WorkflowUserContext actor) => new() { [WorkflowsConstants.InputKeys.Actor] = AccessGateTests.Seal.Bind(actor, Instance) };

    private static readonly WorkflowUserContext Alice = new() { UserId = "u-1", UserName = "alice", Tenant = "acme", Side = CallerSide.Admin };

    [Fact]
    public async Task Each_burst_builds_its_own_caller()
    {
        var rights = new Rights();
        var resolver = Resolver(Factory(rights));
        var workflow = Definition();

        var first = await resolver.ForRunAsync(workflow, InputFor(Alice), Instance, false, TestContext.Current.CancellationToken);
        var second = await resolver.ForRunAsync(workflow, InputFor(Alice), Instance, false, TestContext.Current.CancellationToken);

        Assert.NotSame(first, second);
        Assert.Equal(2, rights.Builds);
    }

    [Fact]
    public async Task A_right_revoked_between_two_bursts_is_gone_on_the_second()
    {
        var rights = new Rights();
        var resolver = Resolver(Factory(rights));
        var workflow = Definition();

        var before = await resolver.ForRunAsync(workflow, InputFor(Alice), Instance, false, TestContext.Current.CancellationToken);
        rights.Granted.Remove("PublishContent");
        rights.Version++;
        var after = await resolver.ForRunAsync(workflow, InputFor(Alice), Instance, false, TestContext.Current.CancellationToken);

        Assert.Contains("PublishContent", before.Permissions);
        Assert.DoesNotContain("PublishContent", after.Permissions);
        Assert.True(after.PermissionVersion > before.PermissionVersion);
    }

    [Fact]
    public async Task A_persisted_actor_as_plain_json_is_refused()
    {
        // The instance input after a tamper (an import, an overwrite through a resume's input):
        // JSON in the actor's shape, with rights it has no place for.
        var resolver = Resolver(Factory(new Rights()));
        var tampered = System.Text.Json.JsonDocument.Parse("""
            {"userId":"u-1","userName":"alice","tenant":"acme","side":"Admin","isSuperUser":true,"permissions":["ManageUsers"],"roles":["Administrator"]}
            """).RootElement;

        await Assert.ThrowsAsync<WorkflowAccessRefusedException>(() =>
            resolver.ForRunAsync(Definition(), new Dictionary<string, object> { [WorkflowsConstants.InputKeys.Actor] = tampered }, Instance, false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_sealed_actor_carries_identity_only()
    {
        var rights = new Rights();
        rights.Granted.Clear();
        var resolver = Resolver(Factory(rights));

        var caller = await resolver.ForRunAsync(Definition(), InputFor(Alice), Instance, false, TestContext.Current.CancellationToken);

        Assert.Equal("u-1", caller.UserId);
        Assert.False(caller.IsSuperUser);
        Assert.Empty(caller.Permissions);
    }

    [Fact]
    public async Task A_sealed_system_actor_never_produces_the_system_caller()
    {
        var resolver = Resolver(Factory(new Rights()));
        var input = InputFor(new WorkflowUserContext { Tenant = "acme", Side = CallerSide.System, IsSystem = true });

        await Assert.ThrowsAsync<WorkflowAccessRefusedException>(() => resolver.ForRunAsync(Definition(), input, Instance, false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_actor_bound_to_another_run_is_refused()
    {
        var resolver = Resolver(Factory(new Rights()));

        await Assert.ThrowsAsync<WorkflowAccessRefusedException>(() => resolver.ForRunAsync(Definition(), InputFor(Alice), "another-instance", false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_system_definition_rebuilds_the_system_caller_per_burst()
    {
        var factory = Factory(new Rights());
        var builds = 0;
        factory.CreateSystemAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                builds++;
                return Task.FromResult(new CallerContext { Tenant = "acme", Side = CallerSide.System, IsSystem = true, IsSuperUser = true, PermissionVersion = builds });
            });
        var resolver = Resolver(factory);

        await resolver.ForRunAsync(Definition(runsAsSystem: true), null, Instance, false, TestContext.Current.CancellationToken);
        await resolver.ForRunAsync(Definition(runsAsSystem: true), null, Instance, false, TestContext.Current.CancellationToken);

        Assert.Equal(2, builds);
    }

    [Fact]
    public async Task A_member_actor_keeps_its_organization_and_side_across_bursts()
    {
        var resolver = Resolver(Factory(new Rights()));
        var member = new WorkflowUserContext { UserId = "u-9", UserName = "mia", Tenant = "acme", Side = CallerSide.Member, OrganizationId = "org-1" };

        var caller = await resolver.ForRunAsync(Definition(), InputFor(member), Instance, false, TestContext.Current.CancellationToken);

        Assert.Equal(CallerSide.Member, caller.Side);
        Assert.Equal("org-1", caller.OrganizationId);
    }
}
