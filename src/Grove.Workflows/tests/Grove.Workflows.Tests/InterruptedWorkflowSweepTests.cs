using Grove.Workflows.Common;
using Grove.Workflows.Common.Models;
using Grove.Workflows.Indexes;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Entities;
using Grove.Workflows.Management.Filters;
using Grove.Workflows.Management.Models;
using Grove.Workflows.Migrations;
using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Options;
using Grove.Workflows.Runtime.Tasks;
using Grove.Workflows.State;
using Grove.Workflows.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace Grove.Workflows.Tests;

// The interrupted-workflow sweep (docs/workflows.md › Interrupted instances): it restarts only the
// instances still marked executing past the inactivity threshold, each once, and its paging ends.
// A sweep that matched every instance and paged in place once queued the same instances millions
// of times a minute and took the dev host to an out-of-memory kill.
public class InterruptedWorkflowSweepTests
{
    [Fact]
    public void The_next_page_starts_one_page_of_items_further()
    {
        var first = PageArgs.FromPage(0, 100);

        Assert.Equal(100, first.Next().Offset);
        Assert.Equal(200, first.Next().Next().Offset);
        Assert.Equal(2, first.Next().Next().Page);
        Assert.Equal(100, first.Next().Limit);
    }

    [Fact]
    public void Unlimited_paging_has_no_next_page()
    {
        Assert.Throws<InvalidOperationException>(() => PageArgs.All.Next());
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(2, 100)]
    [InlineData(100, 100)]
    [InlineData(250, 100)]
    [InlineData(5, 2)]
    public async Task Enumerating_summaries_yields_each_instance_once_and_ends(int total, int batchSize)
    {
        var ids = Enumerable.Range(0, total).Select(i => $"wf-{i:D4}").ToList();
        var store = Substitute.For<IWorkflowInstanceStore>();
        store.SummarizeManyAsync(Arg.Any<WorkflowInstanceFilter>(), Arg.Any<PageArgs>(), Arg.Any<WorkflowInstanceOrder<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var page = call.ArgAt<PageArgs>(1);
                var items = ids.Skip(page.Offset!.Value).Take(page.Limit!.Value).Select(id => new WorkflowInstanceSummary { Id = id }).ToList();
                return new ValueTask<Page<WorkflowInstanceSummary>>(Page.Of<WorkflowInstanceSummary>(items, total));
            });

        var seen = new List<string>();
        await foreach (var summary in store.EnumerateSummariesAsync(new WorkflowInstanceFilter(), batchSize, TestContext.Current.CancellationToken))
        {
            seen.Add(summary.Id);
            Assert.True(seen.Count <= total, "The enumeration did not end.");
        }

        Assert.Equal(ids, seen);
    }

    [Fact]
    public async Task A_sweep_restarts_each_instance_at_most_once_even_when_the_store_repeats_itself()
    {
        // A store whose paging never advances: every page is the same three instances.
        var store = Substitute.For<IWorkflowInstanceStore>();
        store.SummarizeManyAsync(Arg.Any<WorkflowInstanceFilter>(), Arg.Any<PageArgs>(), Arg.Any<WorkflowInstanceOrder<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<Page<WorkflowInstanceSummary>>(Page.Of<WorkflowInstanceSummary>(
                new[] { "a", "b", "c" }.Select(id => new WorkflowInstanceSummary { Id = id }).ToList(), 3)));
        var restarter = Substitute.For<IWorkflowRestarter>();
        var clock = Substitute.For<ISystemClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var task = new RestartInterruptedWorkflowsTask(store, restarter, Microsoft.Extensions.Options.Options.Create(new RuntimeOptions { RestartInterruptedWorkflowsBatchSize = 3 }), clock, NullLogger<RestartInterruptedWorkflowsTask>.Instance);

        await task.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, restarter.ReceivedCalls().Count());
        foreach (var id in new[] { "a", "b", "c" })
            await restarter.Received(1).RestartWorkflowAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_sweep_finds_only_stale_executing_instances_in_the_store()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var stale = now.AddHours(-1);
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var session = fixture.Store.CreateSession())
        {
            var store = fixture.CreateInstanceStore(session);
            for (var i = 0; i < 250; i++)
                await store.AddAsync(Instance($"done-{i:D3}", isExecuting: false, updatedAt: stale), cancellationToken);
            foreach (var id in new[] { "stuck-1", "stuck-2", "stuck-3" })
                await store.AddAsync(Instance(id, isExecuting: true, updatedAt: stale), cancellationToken);
            await store.AddAsync(Instance("running", isExecuting: true, updatedAt: now), cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        await using (var session = fixture.Store.CreateSession())
        {
            var store = fixture.CreateInstanceStore(session);
            var filter = new WorkflowInstanceFilter { IsExecuting = true, BeforeLastUpdated = now.AddMinutes(-5) };

            var interrupted = new List<string>();
            await foreach (var summary in store.EnumerateSummariesAsync(filter, batchSize: 2, cancellationToken))
                interrupted.Add(summary.Id);

            Assert.Equal(["stuck-1", "stuck-2", "stuck-3"], interrupted);

            var all = new List<string>();
            await foreach (var summary in store.EnumerateSummariesAsync(new WorkflowInstanceFilter(), batchSize: 100, cancellationToken))
                all.Add(summary.Id);

            Assert.Equal(254, all.Distinct().Count());
            Assert.Equal(254, all.Count);
        }
    }

    [Fact]
    public async Task Deleting_by_filter_removes_every_match()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var session = fixture.Store.CreateSession())
        {
            var store = fixture.CreateInstanceStore(session);
            for (var i = 0; i < 250; i++)
                await store.AddAsync(Instance($"done-{i:D3}", isExecuting: false, updatedAt: DateTimeOffset.UtcNow), cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        await using (var session = fixture.Store.CreateSession())
        {
            var store = fixture.CreateInstanceStore(session);
            var deleted = await store.DeleteAsync(new WorkflowInstanceFilter(), cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            Assert.Equal(250, deleted);
            Assert.Equal(0, await store.CountAsync(new WorkflowInstanceFilter(), cancellationToken));
        }
    }

    private static WorkflowInstance Instance(string id, bool isExecuting, DateTimeOffset updatedAt) => new()
    {
        Id = id,
        DefinitionId = "definition",
        DefinitionVersionId = "definition-v1",
        Version = 1,
        Status = isExecuting ? WorkflowStatus.Running : WorkflowStatus.Finished,
        SubStatus = isExecuting ? WorkflowSubStatus.Executing : WorkflowSubStatus.Finished,
        IsExecuting = isExecuting,
        CreatedAt = updatedAt,
        UpdatedAt = updatedAt,
        WorkflowState = new WorkflowState { Id = id, IsExecuting = isExecuting },
    };

    private sealed class StoreFixture : IAsyncDisposable
    {
        private readonly string _file;

        private StoreFixture(IStore store, string file)
        {
            Store = store;
            _file = file;
        }

        public IStore Store { get; }

        public static async Task<StoreFixture> CreateAsync()
        {
            var file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var store = await StoreFactory.CreateAndInitializeAsync(new Configuration().UseSqLite($"Data Source={file};Cache=Shared"));
            await store.InitializeCollectionAsync(WorkflowCollections.WorkflowInstances);

            // The schema comes from the module's own migration.
            await using (var session = store.CreateSession())
            {
                var migration = new WorkflowInstanceMigrations { SchemaBuilder = new SchemaBuilder(store.Configuration, await session.BeginTransactionAsync()) };
                Assert.Equal(1, await migration.CreateAsync());
                await session.SaveChangesAsync();
            }

            store.RegisterIndexes<WorkflowInstanceIndexProvider>();
            return new StoreFixture(store, file);
        }

        public YesSqlWorkflowInstanceStore CreateInstanceStore(ISession session)
        {
            var serializer = Substitute.For<IWorkflowStateSerializer>();
            serializer.Serialize(Arg.Any<WorkflowState>()).Returns("{}");
            return new YesSqlWorkflowInstanceStore(session, serializer);
        }

        public ValueTask DisposeAsync()
        {
            Store.Dispose();
            try
            {
                File.Delete(_file);
            }
            catch (IOException)
            {
            }

            return ValueTask.CompletedTask;
        }
    }
}
