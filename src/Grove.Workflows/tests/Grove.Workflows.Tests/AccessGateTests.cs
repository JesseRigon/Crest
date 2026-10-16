using System.Security.Claims;
using Grove.Access;
using Grove.Workflows.Activities;
using Grove.Workflows.CommitStates;
using Grove.Workflows.Common;
using Grove.Workflows.Contexts;
using Grove.Workflows.Management;
using Grove.Workflows.Models;
using Grove.Workflows.Registry;
using Grove.Workflows.Security;
using Grove.Workflows.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Grove.Contents.Management;
using Xunit;

namespace Grove.Workflows.Tests;

// The access gate as engine middleware (docs/operations.md step 4): once per burst the
// caller is set from the definition's system flag or the actor in the instance input; a
// burst with neither is refused; a nested burst (a hook child inside its parent's burst, a
// host that ran the burst as a caller) inherits the caller already set and records it as
// the actor.
public class AccessGateTests
{
    private sealed class Accessor : ICallerContextAccessor
    {
        public CallerContext? Current { get; set; }
    }

    internal static readonly WorkflowActorSeal Seal = new(new EphemeralDataProtectionProvider());

    private static readonly CallerContext Alice = new() { Tenant = "acme", Side = CallerSide.Admin, UserId = "u-1", UserName = "alice" };
    private static readonly CallerContext System = new() { Tenant = "acme", Side = CallerSide.System, IsSystem = true, IsSuperUser = true, UserName = "system" };

    private static async Task<(WorkflowExecutionContext Context, Accessor Accessor)> BurstAsync(bool runsAsSystem, IDictionary<string, object>? input, CallerContext? current = null)
    {
        var factory = Substitute.For<ICallerContextFactory>();
        factory.CreateSystemAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(System));
        factory.CreateAsync(Arg.Any<CallerRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<CallerRequest>()!;
                return Task.FromResult(new CallerContext { Tenant = "acme", Side = request.Side, UserId = request.Principal!.FindFirstValue(ClaimTypes.NameIdentifier), UserName = request.Principal!.Identity!.Name });
            });
        var accessor = new Accessor { Current = current };

        var services = new ServiceCollection();
        services.AddSingleton<ICallerContextAccessor>(accessor);
        services.AddSingleton(Seal);
        services.AddSingleton(sp => new WorkflowCallerResolver(factory, accessor, Seal, Substitute.For<IWorkflowInstanceStore>(), Substitute.For<IWorkflowDefinitionService>()));
        services.AddSingleton(Substitute.For<ISystemClock>());
        services.AddSingleton(Substitute.For<IActivityRegistry>());
        services.AddSingleton(Substitute.For<IActivityRegistryLookupService>());
        services.AddSingleton(Substitute.For<IHasher>());
        services.AddSingleton(Substitute.For<ICommitStateHandler>());
        services.AddSingleton(Substitute.For<IActivitySchedulerFactory>());
        services.AddSingleton(Substitute.For<IIdentityGenerator>());
        var provider = services.BuildServiceProvider();

        var workflow = new Workflow { CustomProperties = runsAsSystem ? new Dictionary<string, object> { [WorkflowsConstants.RunsAsSystemProperty] = true } : new Dictionary<string, object>() };
        workflow.Id = "wf";
        workflow.WorkflowMetadata = new WorkflowMetadata("Nightly");
        var root = new ActivityNode(workflow, "Root");
        var graph = new WorkflowGraph(workflow, root, [root]);
        var context = await WorkflowExecutionContext.CreateAsync(provider, graph, "instance-1", correlationId: null, input: input, cancellationToken: TestContext.Current.CancellationToken);
        return (context, accessor);
    }

    [Fact]
    public async Task A_burst_with_no_actor_and_no_system_flag_is_refused_before_anything_runs()
    {
        var (context, accessor) = await BurstAsync(runsAsSystem: false, input: null);
        var reached = false;
        var gate = new WorkflowAccessGateMiddleware(_ => { reached = true; return ValueTask.CompletedTask; }, NullLogger<WorkflowAccessGateMiddleware>.Instance);

        var refused = await Assert.ThrowsAsync<WorkflowAccessRefusedException>(async () => await gate.InvokeAsync(context));

        Assert.Contains("no actor", refused.Message);
        Assert.Contains("published as system", refused.Message);
        Assert.False(reached);
        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task A_burst_runs_as_the_actor_in_its_input_and_restores_the_scope_after()
    {
        var input = new Dictionary<string, object> { [WorkflowsConstants.InputKeys.Actor] = Seal.Seal(new WorkflowUserContext { UserId = "u-1", UserName = "alice", Tenant = "acme", Side = CallerSide.Admin }) };
        var (context, accessor) = await BurstAsync(runsAsSystem: false, input);
        CallerContext? seen = null;
        var gate = new WorkflowAccessGateMiddleware(_ => { seen = accessor.Current; return ValueTask.CompletedTask; }, NullLogger<WorkflowAccessGateMiddleware>.Instance);

        await gate.InvokeAsync(context);

        Assert.Equal("u-1", seen!.UserId);
        Assert.Equal(CallerSide.Admin, seen.Side);
        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task A_definition_published_as_system_runs_as_the_system_actor_whatever_the_input_says()
    {
        var (context, accessor) = await BurstAsync(runsAsSystem: true, input: null);
        CallerContext? seen = null;
        var gate = new WorkflowAccessGateMiddleware(_ => { seen = accessor.Current; return ValueTask.CompletedTask; }, NullLogger<WorkflowAccessGateMiddleware>.Instance);

        await gate.InvokeAsync(context);

        Assert.True(seen!.IsSystem);
        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task A_nested_burst_inherits_the_caller_already_set_and_records_it_as_the_actor()
    {
        // The input names someone else (a client-supplied actor): the caller of the scope wins.
        var input = new Dictionary<string, object> { [WorkflowsConstants.InputKeys.Actor] = new WorkflowUserContext { UserId = "u-9", UserName = "mallory", Tenant = "acme" } };
        var (context, accessor) = await BurstAsync(runsAsSystem: false, input, current: Alice);
        CallerContext? seen = null;
        var gate = new WorkflowAccessGateMiddleware(_ => { seen = accessor.Current; return ValueTask.CompletedTask; }, NullLogger<WorkflowAccessGateMiddleware>.Instance);

        await gate.InvokeAsync(context);

        Assert.Same(Alice, seen);
        Assert.Same(Alice, accessor.Current);
        Assert.Equal("u-1", Seal.Open(context.Input[WorkflowsConstants.InputKeys.Actor], context.Id, acceptFresh: false)!.UserId);
    }

    [Fact]
    public async Task An_actor_the_server_did_not_seal_is_refused()
    {
        // What a client puts in the input of a dispatch, an import or a resume: plain JSON or an object.
        var forged = global::System.Text.Json.JsonSerializer.SerializeToElement(new WorkflowUserContext { UserId = "u-admin", UserName = "admin", Tenant = "acme", Side = CallerSide.Admin });
        foreach (var value in new object[] { forged, new WorkflowUserContext { UserId = "u-admin" }, "not-a-seal" })
        {
            var (context, accessor) = await BurstAsync(runsAsSystem: false, new Dictionary<string, object> { [WorkflowsConstants.InputKeys.Actor] = value });
            var gate = new WorkflowAccessGateMiddleware(_ => ValueTask.CompletedTask, NullLogger<WorkflowAccessGateMiddleware>.Instance);

            await Assert.ThrowsAsync<WorkflowAccessRefusedException>(async () => await gate.InvokeAsync(context));
            Assert.Null(accessor.Current);
        }
    }

    [Fact]
    public async Task The_first_burst_through_the_runner_accepts_the_fresh_actor()
    {
        // The runner moves a new run from Pending to Executing before the pipeline (and so before
        // the gate) runs; it records the first burst so a fresh sealed actor is still accepted.
        var input = new Dictionary<string, object> { [WorkflowsConstants.InputKeys.Actor] = Seal.Seal(new WorkflowUserContext { UserId = "u-1", UserName = "alice", Tenant = "acme", Side = CallerSide.Admin }) };
        var (context, accessor) = await BurstAsync(runsAsSystem: false, input);
        context.IsFirstBurst = true;
        typeof(WorkflowExecutionContext).GetProperty(nameof(WorkflowExecutionContext.SubStatus))!.SetValue(context, WorkflowSubStatus.Executing);
        CallerContext? seen = null;
        var gate = new WorkflowAccessGateMiddleware(_ => { seen = accessor.Current; return ValueTask.CompletedTask; }, NullLogger<WorkflowAccessGateMiddleware>.Instance);

        await gate.InvokeAsync(context);

        Assert.Equal("u-1", seen!.UserId);
    }

    [Fact]
    public async Task The_first_burst_binds_the_actor_to_its_instance()
    {
        var input = new Dictionary<string, object> { [WorkflowsConstants.InputKeys.Actor] = Seal.Seal(new WorkflowUserContext { UserId = "u-1", UserName = "alice", Tenant = "acme", Side = CallerSide.Admin }) };
        var (context, _) = await BurstAsync(runsAsSystem: false, input);
        var gate = new WorkflowAccessGateMiddleware(_ => ValueTask.CompletedTask, NullLogger<WorkflowAccessGateMiddleware>.Instance);

        await gate.InvokeAsync(context);

        var bound = context.Input[WorkflowsConstants.InputKeys.Actor];
        Assert.Equal("u-1", Seal.Open(bound, context.Id, acceptFresh: false)!.UserId);
        // Copied into another run, it opens for nobody.
        Assert.Null(Seal.Open(bound, "another-instance", acceptFresh: true));
    }

    [Fact]
    public async Task A_later_burst_accepts_only_the_actor_bound_to_its_instance()
    {
        // A fresh actor (or one bound elsewhere) put into a resume's input does not replace the run's.
        var fresh = Seal.Seal(new WorkflowUserContext { UserId = "u-admin", UserName = "admin", Tenant = "acme", Side = CallerSide.Admin });
        var (context, accessor) = await BurstAsync(runsAsSystem: false, new Dictionary<string, object> { [WorkflowsConstants.InputKeys.Actor] = fresh });
        Resumed(context);
        var gate = new WorkflowAccessGateMiddleware(_ => ValueTask.CompletedTask, NullLogger<WorkflowAccessGateMiddleware>.Instance);

        await Assert.ThrowsAsync<WorkflowAccessRefusedException>(async () => await gate.InvokeAsync(context));

        context.Input[WorkflowsConstants.InputKeys.Actor] = Seal.Bind(new WorkflowUserContext { UserId = "u-1", UserName = "alice", Tenant = "acme", Side = CallerSide.Admin }, context.Id);
        CallerContext? seen = null;
        gate = new WorkflowAccessGateMiddleware(_ => { seen = accessor.Current; return ValueTask.CompletedTask; }, NullLogger<WorkflowAccessGateMiddleware>.Instance);
        await gate.InvokeAsync(context);
        Assert.Equal("u-1", seen!.UserId);
    }

    [Fact]
    public async Task A_resume_runs_as_the_runs_actor_not_as_whoever_resumed_it()
    {
        var actor = new WorkflowUserContext { UserId = "u-1", UserName = "alice", Tenant = "acme", Side = CallerSide.Admin };
        var resumer = new CallerContext { Tenant = "acme", Side = CallerSide.Admin, UserId = "u-7", UserName = "approver" };
        var (context, accessor) = await BurstAsync(runsAsSystem: false, new Dictionary<string, object>(), current: resumer);
        context.Input[WorkflowsConstants.InputKeys.Actor] = Seal.Bind(actor, context.Id);
        Resumed(context);
        CallerContext? seen = null;
        var gate = new WorkflowAccessGateMiddleware(_ => { seen = accessor.Current; return ValueTask.CompletedTask; }, NullLogger<WorkflowAccessGateMiddleware>.Instance);

        await gate.InvokeAsync(context);

        Assert.Equal("u-1", seen!.UserId);
        Assert.Same(resumer, accessor.Current);
    }

    [Fact]
    public async Task The_system_caller_is_not_inherited_by_a_flow_not_published_as_system()
    {
        // A background task (publish later) changed content; the subscribed flow has no actor
        // of its own and must not run as the tenant super user.
        var (context, accessor) = await BurstAsync(runsAsSystem: false, input: null, current: System);
        var gate = new WorkflowAccessGateMiddleware(_ => ValueTask.CompletedTask, NullLogger<WorkflowAccessGateMiddleware>.Instance);

        await Assert.ThrowsAsync<WorkflowAccessRefusedException>(async () => await gate.InvokeAsync(context));
        Assert.Same(System, accessor.Current);
    }

    [Fact]
    public async Task A_flow_published_as_system_inherits_the_system_caller()
    {
        var (context, accessor) = await BurstAsync(runsAsSystem: true, input: null, current: System);
        CallerContext? seen = null;
        var gate = new WorkflowAccessGateMiddleware(_ => { seen = accessor.Current; return ValueTask.CompletedTask; }, NullLogger<WorkflowAccessGateMiddleware>.Instance);

        await gate.InvokeAsync(context);

        Assert.Same(System, seen);
    }

    [Fact]
    public async Task An_anonymous_run_is_refused_unless_the_definition_accepts_one()
    {
        var anonymous = CallerContext.Anonymous("acme", CallerSide.Site);
        var (context, accessor) = await BurstAsync(runsAsSystem: false, input: null, current: anonymous);
        var gate = new WorkflowAccessGateMiddleware(_ => ValueTask.CompletedTask, NullLogger<WorkflowAccessGateMiddleware>.Instance);

        var refused = await Assert.ThrowsAsync<WorkflowAccessRefusedException>(async () => await gate.InvokeAsync(context));
        Assert.Contains("does not accept anonymous runs", refused.Message);
        Assert.Same(anonymous, accessor.Current);
    }

    [Fact]
    public void An_anonymous_run_is_admitted_only_from_a_page_endpoint_of_a_definition_that_accepts_one()
    {
        var anonymous = CallerContext.Anonymous("acme", CallerSide.Site);
        var accepting = new Workflow { CustomProperties = new Dictionary<string, object> { [WorkflowsConstants.AcceptsAnonymousRunsProperty] = true } };
        accepting.WorkflowMetadata = new WorkflowMetadata("Contact form");
        var plain = new Workflow { CustomProperties = new Dictionary<string, object>() };
        plain.WorkflowMetadata = new WorkflowMetadata("Internal");

        Assert.Null(WorkflowCallerResolver.AnonymousRefusal(anonymous, accepting, firstBurst: true, startsAtPageEndpoint: true));
        Assert.Contains("page-URL HTTP endpoint", WorkflowCallerResolver.AnonymousRefusal(anonymous, accepting, firstBurst: true, startsAtPageEndpoint: false));
        // A later burst keeps the bound anonymous actor of a run admitted that way.
        Assert.Null(WorkflowCallerResolver.AnonymousRefusal(anonymous, accepting, firstBurst: false, startsAtPageEndpoint: false));
        Assert.NotNull(WorkflowCallerResolver.AnonymousRefusal(anonymous, plain, firstBurst: true, startsAtPageEndpoint: true));
        // A signed-in caller, an application and the system are never refused for it.
        Assert.Null(WorkflowCallerResolver.AnonymousRefusal(Alice, plain, firstBurst: true, startsAtPageEndpoint: false));
        Assert.Null(WorkflowCallerResolver.AnonymousRefusal(System, plain, firstBurst: true, startsAtPageEndpoint: false));
    }

    [Fact]
    public async Task Accepting_anonymous_runs_takes_ManageShippedWorkflows()
    {
        var contentManager = Substitute.For<IContentManager>();
        contentManager.GetAsync(Arg.Any<string>(), Arg.Any<VersionOptions>()).Returns(Task.FromResult<ContentItem>(null!));
        var authorizer = Substitute.For<IWorkflowDefinitionAuthorizer>();
        authorizer.AuthorizeAsync(Arg.Any<Grove.Security.Permissions.Permission>(), Arg.Any<WorkflowDefinitionAccessResource>())
            .Returns(call => Task.FromResult(call.Arg<Grove.Security.Permissions.Permission>()!.Name != WorkflowsConstants.Permissions.ManageShipped));
        var guard = new WorkflowOwnershipGuard(contentManager, null!, new Accessor { Current = Alice }, authorizer);
        var definition = new Management.Entities.WorkflowDefinition { DefinitionId = "def-1", CustomProperties = new Dictionary<string, object> { [WorkflowsConstants.AcceptsAnonymousRunsProperty] = true } };

        var denied = await Assert.ThrowsAsync<WorkflowChangeDeniedException>(() => guard.PrepareAsync(definition, WorkflowChange.Publish));
        Assert.Contains("anonymous", denied.Message);
    }

    private static void Resumed(WorkflowExecutionContext context) =>
        typeof(WorkflowExecutionContext).GetProperty(nameof(WorkflowExecutionContext.SubStatus))!.SetValue(context, WorkflowSubStatus.Suspended);

    [Fact]
    public void The_declared_permission_rides_on_the_descriptor()
    {
        var descriptor = new ActivityDescriptor { ClrType = typeof(Connectors.CallConnector) };
        new RequiredPermissionDescriptorModifier().Modify(descriptor);
        Assert.Equal(Grove.Data.Connectors.ConnectorsPermissionNames.UseConnections, ActivityAccessGateMiddleware.RequiredPermission(descriptor));

        var copy = new ActivityDescriptor { ClrType = typeof(Contents.CopyFields) };
        new RequiredPermissionDescriptorModifier().Modify(copy);
        Assert.Equal("EditContent", ActivityAccessGateMiddleware.RequiredPermission(copy));

        var plain = new ActivityDescriptor { ClrType = typeof(RequirePermission) };
        new RequiredPermissionDescriptorModifier().Modify(plain);
        Assert.Null(ActivityAccessGateMiddleware.RequiredPermission(plain));
    }

    [Fact]
    public async Task Publishing_as_system_takes_ManageShippedWorkflows()
    {
        var contentManager = Substitute.For<IContentManager>();
        contentManager.GetAsync(Arg.Any<string>(), Arg.Any<VersionOptions>()).Returns(Task.FromResult<ContentItem>(null!));
        var callers = new Accessor { Current = Alice };
        var authorizer = Substitute.For<IWorkflowDefinitionAuthorizer>();
        authorizer.AuthorizeAsync(Arg.Any<Grove.Security.Permissions.Permission>(), Arg.Any<WorkflowDefinitionAccessResource>())
            .Returns(call => Task.FromResult(call.Arg<Grove.Security.Permissions.Permission>()!.Name != WorkflowsConstants.Permissions.ManageShipped));
        var guard = new WorkflowOwnershipGuard(contentManager, null!, callers, authorizer);
        var definition = new Management.Entities.WorkflowDefinition { DefinitionId = "def-1", CustomProperties = new Dictionary<string, object> { [WorkflowsConstants.RunsAsSystemProperty] = true } };

        var denied = await Assert.ThrowsAsync<WorkflowChangeDeniedException>(() => guard.PrepareAsync(definition, WorkflowChange.Publish));
        Assert.Contains(WorkflowsConstants.Permissions.ManageShipped, denied.Message);

        // Without the flag, Publish alone is enough; with the flag and the permission, it goes through.
        await guard.PrepareAsync(new Management.Entities.WorkflowDefinition { DefinitionId = "def-1", CustomProperties = new Dictionary<string, object>() }, WorkflowChange.Publish);
        authorizer.AuthorizeAsync(Arg.Any<Grove.Security.Permissions.Permission>(), Arg.Any<WorkflowDefinitionAccessResource>()).Returns(Task.FromResult(true));
        await guard.PrepareAsync(definition, WorkflowChange.Publish);
        Assert.True(WorkflowCallerResolver.RunsAsSystem(definition.CustomProperties));
    }

    [Fact]
    public async Task A_change_with_no_caller_is_refused()
    {
        // Outside a request or a burst (and outside the importer's system scope) nobody may
        // change a definition: the guard no longer passes when it finds no signed-in user.
        var contentManager = Substitute.For<IContentManager>();
        contentManager.GetAsync(Arg.Any<string>(), Arg.Any<VersionOptions>()).Returns(Task.FromResult<ContentItem>(null!));
        var authorizer = Substitute.For<IWorkflowDefinitionAuthorizer>();
        authorizer.AuthorizeAsync(Arg.Any<Grove.Security.Permissions.Permission>(), Arg.Any<WorkflowDefinitionAccessResource>()).Returns(Task.FromResult(true));
        var guard = new WorkflowOwnershipGuard(contentManager, null!, new Accessor(), authorizer);

        await Assert.ThrowsAsync<WorkflowChangeDeniedException>(() => guard.PrepareAsync(new Management.Entities.WorkflowDefinition { DefinitionId = "def-1", CustomProperties = new Dictionary<string, object>() }, WorkflowChange.Save));
    }
}

// The registry's query kind: one engine descriptor per query, typed from the query's
// parameters and columns, carrying the schema and the per-query execute permission.
public class QueryActivityProviderTests
{
    [Fact]
    public void A_query_descriptor_becomes_a_run_query_activity_descriptor()
    {
        var query = new Grove.Data.Queries.QueryDescriptor("open-invoices", "Open invoices",
            [new Grove.Data.Queries.Structured.QueryParameter("CustomerId", Grove.Data.Queries.Structured.QueryParameterType.String, Required: true), new Grove.Data.Queries.Structured.QueryParameter("PageSize", Grove.Data.Queries.Structured.QueryParameterType.Int)],
            [new Grove.Data.Queries.QueryColumn("Number", typeof(string)), new Grove.Data.Queries.QueryColumn("Amount", typeof(decimal))],
            "structured");
        var template = new ActivityDescriptor { Inputs = [new InputDescriptor { Name = "PageSize", Type = typeof(int?) }], Outputs = [new OutputDescriptor { Name = "Rows", Type = typeof(IList<object>) }] };

        var descriptor = Queries.QueryActivityProvider.Describe(query, template);

        Assert.Equal(typeof(Queries.RunQuery), descriptor.ClrType);
        Assert.Equal("Grove.Data.Queries.open_invoices", descriptor.TypeName);
        Assert.Equal("open-invoices", descriptor.CustomProperties[WorkflowsConstants.QueryNameDescriptorProperty]);
        Assert.Equal("ExecuteApi_open-invoices", ActivityAccessGateMiddleware.RequiredPermission(descriptor));
        Assert.True(descriptor.CustomProperties.ContainsKey(WorkflowsConstants.QuerySchemaDescriptorProperty));

        // Parameters are synthetic inputs beside the class's own, never colliding with them.
        var synthetic = descriptor.Inputs.Where(i => i.IsSynthetic).ToList();
        Assert.Equal(["CustomerId", "PageSize"], synthetic.Select(Queries.QueryActivityProvider.ParameterName));
        Assert.Equal([typeof(string), typeof(int)], synthetic.Select(i => i.Type));
        Assert.Equal(3, descriptor.Inputs.Count);
        Assert.Equal(["Number", "Amount"], descriptor.Outputs.Where(o => o.IsSynthetic).Select(Queries.QueryActivityProvider.ParameterName));

        var activity = (Queries.RunQuery)descriptor.Constructor(new ActivityConstructorContext(descriptor, type => new ActivityConstructionResult((IActivity)Activator.CreateInstance(type)!))).Activity;
        Assert.Equal("open-invoices", activity.QueryName);
        Assert.Equal(descriptor.TypeName, activity.Type);
    }
}
