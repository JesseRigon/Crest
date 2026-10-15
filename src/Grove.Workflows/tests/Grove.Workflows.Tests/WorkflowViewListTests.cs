using System.Text;
using System.Text.Json;
using Grove.Access;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Filters;
using Grove.Workflows.Management.Models;
using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Entities;
using Grove.Workflows.Runtime.Filters;
using Grove.Workflows.SasTokens.Contracts;
using Grove.Workflows.Security;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Grove.Workflows.Tests;

// The third list, View (docs/workflows.md › Ownership and permissions, ruled 2026-10-13):
// who may read a definition's instances, beside Edit and Run, none implying another; an
// empty View list is a tenant-wide definition. And the engine API's instance operations
// decided against the definitions they reach (the access.md defect): reads against View,
// changes, bulk actions, resumes, task completions and event triggers against Run.
public class WorkflowViewListTests
{
    private static readonly CallerContext Alice = new() { Tenant = "acme", Side = CallerSide.Admin, UserId = "u-1", UserName = "alice", Roles = new HashSet<string>(["Editor"], StringComparer.OrdinalIgnoreCase) };
    private static readonly CallerContext Admin = new() { Tenant = "acme", Side = CallerSide.Admin, UserId = "u-0", UserName = "root", IsSuperUser = true };

    private static bool IsDefinition(object? resource, string definitionId) =>
        resource is WorkflowDefinitionAccessResource definition && definition.DefinitionId == definitionId;

    private static bool HashContains(string? hash, string text) => hash is not null && hash.Contains(text, StringComparison.Ordinal);

    private static WorkflowDefinitionAccessResource Resource(string id, string[]? edit = null, string[]? run = null, string[]? view = null) =>
        new(id, edit ?? [], run ?? [], view ?? []);

    [Fact]
    public void The_view_list_round_trips_beside_the_other_two()
    {
        var stored = JsonSerializer.Deserialize<Dictionary<string, object>>("""{"Grove.Access.View":["Auditors"," bob "],"Grove.Access.Run":"Operators"}""")!;
        var resource = WorkflowDefinitionAccessResource.From("d", stored);
        Assert.Equal(["Auditors", "bob"], resource.View);
        Assert.Equal(["Operators"], resource.Run);
        Assert.Empty(resource.Edit);
        Assert.Equal(["Auditors", "bob"], resource.ToModel().View);

        var properties = new Dictionary<string, object>();
        WorkflowDefinitionAccessResource.Stamp(properties, new WorkflowDefinitionAccessModel([], [], ["Auditors"]));
        Assert.Equal(["Auditors"], WorkflowDefinitionAccessResource.From("d", properties).View);
        WorkflowDefinitionAccessResource.Stamp(properties, new WorkflowDefinitionAccessModel([], []));
        Assert.False(properties.ContainsKey(WorkflowsConstants.AccessViewProperty));
    }

    [Fact]
    public void Edit_and_Run_do_not_imply_View_and_an_empty_list_admits_everyone()
    {
        var narrowed = Resource("d", edit: ["alice"], run: ["alice"], view: ["Auditors"]);
        Assert.True(narrowed.Admits(Alice, WorkflowsConstants.Permissions.Edit));
        Assert.True(narrowed.Admits(Alice, WorkflowsConstants.Permissions.Run));
        Assert.False(narrowed.Admits(Alice, WorkflowsConstants.Permissions.View));
        Assert.True(narrowed.Admits(Admin, WorkflowsConstants.Permissions.View));

        var byRole = Resource("d", view: ["Editor"]);
        Assert.True(byRole.Admits(Alice, WorkflowsConstants.Permissions.View));

        var global = Resource("d", edit: ["Managers"], run: ["Operators"]);
        Assert.True(global.Admits(Alice, WorkflowsConstants.Permissions.View));
        Assert.False(global.Admits(Alice, WorkflowsConstants.Permissions.Run));
    }

    [Fact]
    public void The_ceiling_vetoes_an_instance_read_outside_the_View_list()
    {
        var ceiling = new WorkflowDefinitionAccessCeiling();
        var narrowed = Resource("d", view: ["Auditors"]);

        Assert.Contains("read its instances", ceiling.Deny(Alice, WorkflowsConstants.Permissions.View, narrowed));
        Assert.Null(ceiling.Deny(Alice, WorkflowsConstants.Permissions.Run, narrowed));
        Assert.Null(ceiling.Deny(Alice, WorkflowsConstants.Permissions.View, Resource("d")));
        Assert.Null(ceiling.Deny(Alice, WorkflowsConstants.Permissions.View, null));
    }

    // The engine API's instance operations, through the real EngineInstanceAccess over
    // substituted stores: the decision allows every permission (the lists are what narrow).

    private sealed class World
    {
        public IAccessDecision Decision { get; } = Substitute.For<IAccessDecision>();
        public IWorkflowDefinitionAccessReader Lists { get; } = Substitute.For<IWorkflowDefinitionAccessReader>();
        public IWorkflowInstanceStore Instances { get; } = Substitute.For<IWorkflowInstanceStore>();
        public IWorkflowDefinitionStore Definitions { get; } = Substitute.For<IWorkflowDefinitionStore>();
        public IActivityExecutionStore ActivityExecutions { get; } = Substitute.For<IActivityExecutionStore>();
        public IBookmarkStore Bookmarks { get; } = Substitute.For<IBookmarkStore>();
        public ITriggerStore Triggers { get; } = Substitute.For<ITriggerStore>();
        public IStimulusHasher Hasher { get; } = Substitute.For<IStimulusHasher>();
        public ITokenService Tokens { get; } = Substitute.For<ITokenService>();

        public World(params WorkflowDefinitionAccessResource[] definitions)
        {
            Decision.DecideAsync(Arg.Any<CallerContext>(), Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>()).Returns(AccessDecision.Allowed);
            Lists.ListAsync(Arg.Any<CancellationToken>()).Returns(definitions.ToList());
            Lists.GetAsync(Arg.Any<string>()).Returns(call => definitions.FirstOrDefault(d => d.DefinitionId == call.Arg<string>()));
            Instances.SummarizeManyAsync(Arg.Any<WorkflowInstanceFilter>(), Arg.Any<CancellationToken>())
                .Returns(call => call.Arg<WorkflowInstanceFilter>()!.Ids!.Where(InstanceDefinitions.ContainsKey).Select(id => new WorkflowInstanceSummary { Id = id, DefinitionId = InstanceDefinitions[id] }).ToList());
            // A hash nothing waits on finds nothing, as the stores answer.
            Bookmarks.FindManyAsync(Arg.Any<BookmarkFilter>(), Arg.Any<CancellationToken>()).Returns(Enumerable.Empty<StoredBookmark>());
            Triggers.FindManyAsync(Arg.Any<TriggerFilter>(), Arg.Any<CancellationToken>()).Returns(Enumerable.Empty<StoredTrigger>());
            Hasher.Hash(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>()).Returns(call => $"{call[0]}:{JsonSerializer.Serialize(call[1])}");
        }

        /// <summary>Instance id → definition id.</summary>
        public Dictionary<string, string> InstanceDefinitions { get; } = new(StringComparer.Ordinal);

        public EngineInstanceAccess Access() => new(Decision, Lists, Instances, Definitions, ActivityExecutions, Bookmarks, Triggers, Hasher, Tokens);
    }

    /// <summary>The gate's instance access for the API middleware tests: the lists given, every instance of definition "def-1".</summary>
    internal static EngineInstanceAccess InstanceAccess(IAccessDecision decision, IWorkflowDefinitionAccessReader lists)
    {
        var instances = Substitute.For<IWorkflowInstanceStore>();
        instances.SummarizeManyAsync(Arg.Any<WorkflowInstanceFilter>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<WorkflowInstanceFilter>()!.Ids!.Select(id => new WorkflowInstanceSummary { Id = id, DefinitionId = "def-1" }).ToList());
        return new EngineInstanceAccess(decision, lists, instances, Substitute.For<IWorkflowDefinitionStore>(), Substitute.For<IActivityExecutionStore>(), Substitute.For<IBookmarkStore>(), Substitute.For<ITriggerStore>(), Substitute.For<IStimulusHasher>(), Substitute.For<ITokenService>());
    }

    private static DefaultHttpContext Request(string method, string path, string? jsonBody = null, string? query = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/grove-workflows/api" + path;
        if (query is not null)
        {
            context.Request.QueryString = new QueryString(query);
        }

        if (jsonBody is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(jsonBody);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
            context.Request.ContentType = "application/json";
        }

        return context;
    }

    private static Task<string?> CheckAsync(World world, DefaultHttpContext context, CallerContext? caller = null) =>
        world.Access().CheckAsync(context, context.Request.Path.Value!["/grove-workflows/api".Length..], caller ?? Alice, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("GET", "/workflow-instances/i-1")]
    [InlineData("GET", "/workflow-instances/i-1/journal")]
    [InlineData("POST", "/workflow-instances/i-1/journal")]
    [InlineData("GET", "/workflow-instances/i-1/variables")]
    [InlineData("GET", "/workflow-instances/i-1/execution-state")]
    [InlineData("GET", "/workflow-instances/i-1/export")]
    public async Task An_instance_read_takes_View_against_the_definitions_View_list(string method, string path)
    {
        var world = new World(Resource("def-1", view: ["Auditors"]), Resource("def-2"));
        world.InstanceDefinitions["i-1"] = "def-1";
        world.InstanceDefinitions["i-2"] = "def-2";

        Assert.Contains("read its instances", await CheckAsync(world, Request(method, path)));
        Assert.Null(await CheckAsync(world, Request(method, path.Replace("i-1", "i-2"))));
        Assert.Null(await CheckAsync(world, Request(method, path), Admin));
        await world.Decision.Received().DecideAsync(Alice, WorkflowsConstants.Permissions.View, Arg.Is<object?>(r => IsDefinition(r, "def-1")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activity_executions_are_read_through_their_instance()
    {
        var world = new World(Resource("def-1", view: ["Auditors"]));
        world.InstanceDefinitions["i-1"] = "def-1";
        world.ActivityExecutions.FindAsync(Arg.Is<ActivityExecutionRecordFilter>(f => f!.Id == "rec-1"), Arg.Any<CancellationToken>()).Returns(new ActivityExecutionRecord { Id = "rec-1", WorkflowInstanceId = "i-1" });

        Assert.NotNull(await CheckAsync(world, Request("GET", "/activity-executions/list", query: "?workflowInstanceId=i-1&activityNodeId=a")));
        Assert.NotNull(await CheckAsync(world, Request("POST", "/activity-executions/report", """{"workflowInstanceId":"i-1","activityNodeIds":[]}""")));
        Assert.NotNull(await CheckAsync(world, Request("GET", "/activity-executions/rec-1")));
        Assert.NotNull(await CheckAsync(world, Request("GET", "/activity-execution-summaries/list", query: "?workflowInstanceId=i-1")));
    }

    [Theory]
    [InlineData("DELETE", "/workflow-instances/i-1")]
    [InlineData("POST", "/workflow-instances/i-1/variables")]
    [InlineData("POST", "/cancel/workflow-instances/i-1")]
    public async Task Changing_an_instance_takes_Run_against_the_definitions_Run_list(string method, string path)
    {
        var world = new World(Resource("def-1", run: ["Operators"], view: ["alice"]));
        world.InstanceDefinitions["i-1"] = "def-1";

        Assert.Contains("may not run", await CheckAsync(world, Request(method, path)));
        await world.Decision.Received().DecideAsync(Alice, WorkflowsConstants.Permissions.Run, Arg.Is<object?>(r => IsDefinition(r, "def-1")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Bulk_actions_take_Run_for_every_instance_or_definition_named_and_for_all_when_none_is()
    {
        var world = new World(Resource("def-1", run: ["Operators"]), Resource("def-2"));
        world.InstanceDefinitions["i-1"] = "def-1";
        world.InstanceDefinitions["i-2"] = "def-2";

        Assert.NotNull(await CheckAsync(world, Request("POST", "/bulk-actions/cancel/workflow-instances/", """{"ids":["i-2","i-1"]}""")));
        Assert.Null(await CheckAsync(world, Request("POST", "/bulk-actions/cancel/workflow-instances/", """{"ids":["i-2"]}""")));
        Assert.NotNull(await CheckAsync(world, Request("POST", "/bulk-actions/delete/workflow-instances", """{"workflowDefinitionId":"def-1"}""")));
        Assert.Null(await CheckAsync(world, Request("POST", "/bulk-actions/delete/workflow-instances", """{"workflowDefinitionIds":["def-2"]}""")));
        Assert.NotNull(await CheckAsync(world, Request("POST", "/bulk-actions/delete/workflow-instances", """{}""")));
        Assert.Null(await CheckAsync(world, Request("POST", "/bulk-actions/delete/workflow-instances", """{}"""), Admin));
    }

    [Fact]
    public async Task A_bulk_export_takes_View_and_the_body_stays_readable_for_the_engine()
    {
        var world = new World(Resource("def-1", view: ["Auditors"]), Resource("def-2"));
        world.InstanceDefinitions["i-1"] = "def-1";
        world.InstanceDefinitions["i-2"] = "def-2";
        var context = Request("POST", "/bulk-actions/export/workflow-instances", """{"ids":["i-2"]}""");

        Assert.Null(await CheckAsync(world, context));
        Assert.NotNull(await CheckAsync(world, Request("POST", "/bulk-actions/export/workflow-instances", """{"ids":["i-1"]}""")));
        Assert.Equal("""{"ids":["i-2"]}""", await new StreamReader(context.Request.Body).ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_instance_list_is_narrowed_to_the_definitions_whose_View_list_admits_the_caller()
    {
        var world = new World(Resource("def-1", view: ["Auditors"]), Resource("def-2"), Resource("def-3", view: ["Editor"]));

        var unfiltered = Request("GET", "/workflow-instances", query: "?page=0&pageSize=20");
        Assert.Null(await CheckAsync(world, unfiltered));
        Assert.Equal(["def-2", "def-3"], unfiltered.Request.Query["definitionIds"].Select(v => v ?? string.Empty).ToArray());
        Assert.Equal("0", unfiltered.Request.Query["page"].ToString());

        var filtered = Request("GET", "/workflow-instances", query: "?definitionId=def-3");
        Assert.Null(await CheckAsync(world, filtered));
        Assert.Equal(["def-3"], filtered.Request.Query["definitionIds"].Select(v => v ?? string.Empty).ToArray());
        Assert.False(filtered.Request.Query.ContainsKey("definitionId"));

        Assert.NotNull(await CheckAsync(world, Request("GET", "/workflow-instances", query: "?definitionId=def-1")));

        var posted = Request("POST", "/workflow-instances", """{"page":1,"definitionIds":["def-1","def-2"]}""");
        Assert.Null(await CheckAsync(world, posted));
        var body = JsonDocument.Parse(await new StreamReader(posted.Request.Body).ReadToEndAsync(TestContext.Current.CancellationToken)).RootElement;
        Assert.Equal(["def-2"], body.GetProperty("definitionIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(1, body.GetProperty("page").GetInt32());

        // Nothing excludes the super user, so the request is left as it was.
        var admin = Request("GET", "/workflow-instances");
        Assert.Null(await CheckAsync(world, admin, Admin));
        Assert.False(admin.Request.Query.ContainsKey("definitionIds"));
    }

    [Fact]
    public async Task A_bookmark_resume_takes_Run_for_the_instance_its_token_names()
    {
        var world = new World(Resource("def-1", run: ["Operators"]));
        world.InstanceDefinitions["i-1"] = "def-1";
        world.Tokens.TryDecryptToken("tok", out Arg.Any<BookmarkTokenPayload>()).Returns(call =>
        {
            call[1] = new BookmarkTokenPayload("b-1", "i-1");
            return true;
        });

        Assert.NotNull(await CheckAsync(world, Request("POST", "/bookmarks/resume", query: "?t=tok")));
        Assert.Null(await CheckAsync(world, Request("POST", "/bookmarks/resume", query: "?t=bad")));
    }

    [Fact]
    public async Task A_task_completion_takes_Run_for_the_instances_waiting_on_the_task()
    {
        var world = new World(Resource("def-1", run: ["Operators"]));
        world.InstanceDefinitions["i-1"] = "def-1";
        world.Bookmarks.FindManyAsync(Arg.Is<BookmarkFilter>(f => HashContains(f!.Hash, "task-1")), Arg.Any<CancellationToken>())
            .Returns([new StoredBookmark { Id = "b-1", WorkflowInstanceId = "i-1" }]);

        Assert.NotNull(await CheckAsync(world, Request("POST", "/tasks/task-1/complete", """{"result":1}""")));
        Assert.Null(await CheckAsync(world, Request("POST", "/tasks/task-9/complete", """{"result":1}""")));
    }

    [Fact]
    public async Task An_event_trigger_takes_Run_for_what_it_would_start_or_resume()
    {
        var world = new World(Resource("def-1", run: ["Operators"]), Resource("def-2"));
        world.InstanceDefinitions["i-2"] = "def-2";
        world.Triggers.FindManyAsync(Arg.Is<TriggerFilter>(f => HashContains(f!.Hash, "order-placed")), Arg.Any<CancellationToken>())
            .Returns([new StoredTrigger { Id = "t-1", WorkflowDefinitionId = "def-1" }]);
        world.Bookmarks.FindManyAsync(Arg.Is<BookmarkFilter>(f => HashContains(f!.Hash, "invoice-paid")), Arg.Any<CancellationToken>())
            .Returns([new StoredBookmark { Id = "b-2", WorkflowInstanceId = "i-2" }]);

        Assert.NotNull(await CheckAsync(world, Request("POST", "/events/order-placed/trigger", """{"input":{}}""")));
        Assert.Null(await CheckAsync(world, Request("POST", "/events/invoice-paid/trigger", """{"input":{}}""")));
        Assert.Null(await CheckAsync(world, Request("POST", "/events/order-placed/trigger", """{"workflowInstanceId":"i-2"}""")));
    }
}
