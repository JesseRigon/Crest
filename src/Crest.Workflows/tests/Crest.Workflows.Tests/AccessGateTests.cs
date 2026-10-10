using System.Security.Claims;
using Crest.Access;
using Crest.Workflows.Activities;
using Crest.Workflows.CommitStates;
using Crest.Workflows.Common;
using Crest.Workflows.Contexts;
using Crest.Workflows.Management;
using Crest.Workflows.Models;
using Crest.Workflows.Registry;
using Crest.Workflows.Security;
using Crest.Workflows.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Crest.ContentManagement;
using Xunit;

namespace Crest.Workflows.Tests;

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
        services.AddSingleton(sp => new WorkflowCallerResolver(factory, accessor, Substitute.For<IHttpContextAccessor>(), Substitute.For<IWorkflowInstanceStore>(), Substitute.For<IWorkflowDefinitionService>()));
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
        var input = new Dictionary<string, object> { [WorkflowsConstants.InputKeys.Actor] = new WorkflowUserContext { UserId = "u-1", UserName = "alice", Tenant = "acme", Side = CallerSide.Admin } };
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
        Assert.Equal("u-1", ((WorkflowUserContext)context.Input[WorkflowsConstants.InputKeys.Actor]).UserId);
    }

    [Fact]
    public void The_declared_permission_rides_on_the_descriptor()
    {
        var descriptor = new ActivityDescriptor { ClrType = typeof(Connectors.CallConnector) };
        new RequiredPermissionDescriptorModifier().Modify(descriptor);
        Assert.Equal(WorkflowsConstants.Permissions.UseConnections, ActivityAccessGateMiddleware.RequiredPermission(descriptor));

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
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "Test")) };
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);
        var authorizer = Substitute.For<IWorkflowDefinitionAuthorizer>();
        authorizer.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Crest.Security.Permissions.Permission>(), Arg.Any<WorkflowDefinitionAccessResource>())
            .Returns(call => Task.FromResult(call.Arg<Crest.Security.Permissions.Permission>()!.Name != WorkflowsConstants.Permissions.ManageShipped));
        var guard = new WorkflowOwnershipGuard(contentManager, null!, httpContextAccessor, authorizer);
        var definition = new Management.Entities.WorkflowDefinition { DefinitionId = "def-1", CustomProperties = new Dictionary<string, object> { [WorkflowsConstants.RunsAsSystemProperty] = true } };

        var denied = await Assert.ThrowsAsync<WorkflowChangeDeniedException>(() => guard.PrepareAsync(definition, WorkflowChange.Publish));
        Assert.Contains(WorkflowsConstants.Permissions.ManageShipped, denied.Message);

        // Without the flag, Publish alone is enough; with the flag and the permission, it goes through.
        await guard.PrepareAsync(new Management.Entities.WorkflowDefinition { DefinitionId = "def-1", CustomProperties = new Dictionary<string, object>() }, WorkflowChange.Publish);
        authorizer.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Crest.Security.Permissions.Permission>(), Arg.Any<WorkflowDefinitionAccessResource>()).Returns(Task.FromResult(true));
        await guard.PrepareAsync(definition, WorkflowChange.Publish);
        Assert.True(WorkflowCallerResolver.RunsAsSystem(definition.CustomProperties));
    }
}

// The registry's query kind: one engine descriptor per query, typed from the query's
// parameters and columns, carrying the schema and the per-query execute permission.
public class QueryActivityProviderTests
{
    [Fact]
    public void A_query_descriptor_becomes_a_run_query_activity_descriptor()
    {
        var query = new Crest.Queries.QueryDescriptor("open-invoices", "Open invoices",
            [new Crest.Queries.Structured.QueryParameter("CustomerId", Crest.Queries.Structured.QueryParameterType.String, Required: true), new Crest.Queries.Structured.QueryParameter("PageSize", Crest.Queries.Structured.QueryParameterType.Int)],
            [new Crest.Queries.QueryColumn("Number", typeof(string)), new Crest.Queries.QueryColumn("Amount", typeof(decimal))],
            "structured");
        var template = new ActivityDescriptor { Inputs = [new InputDescriptor { Name = "PageSize", Type = typeof(int?) }], Outputs = [new OutputDescriptor { Name = "Rows", Type = typeof(IList<object>) }] };

        var descriptor = Queries.QueryActivityProvider.Describe(query, template);

        Assert.Equal(typeof(Queries.RunQuery), descriptor.ClrType);
        Assert.Equal("Crest.Queries.open_invoices", descriptor.TypeName);
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
