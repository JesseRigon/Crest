using Crest.Workflows.Management;
using Crest.Workflows.Models;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Options;
using Crest.Workflows.Runtime.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace Crest.Workflows.Units;

public static class BackgroundJobStatuses
{
    /// <summary>Recorded in a unit; scheduled once that unit's engine state is saved.</summary>
    public const string Created = "created";
    /// <summary>Waiting for the unit to commit; runs right after.</summary>
    public const string Scheduled = "scheduled";
    public const string Running = "running";
    public const string Done = "done";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

/// <summary>
/// The durable record behind an engine background activity (docs/workflows.md › Posting on
/// workflows): the engine's own scheduler keeps its jobs in memory and runs them in a bare
/// scope; this one is written in the unit that scheduled the activity, so it commits with the
/// bookmark or not at all, survives the process, and carries the idempotency key the
/// receiver of an external call can de-duplicate on.
/// </summary>
public sealed class WorkflowBackgroundJob
{
    public long Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public string ActivityNodeId { get; set; } = string.Empty;
    public string BookmarkId { get; set; } = string.Empty;

    /// <summary>Instance, node and bookmark: stable across retries of one scheduled run, different for every new one.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public string Status { get; set; } = BackgroundJobStatuses.Created;
    public int Attempts { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? Error { get; set; }
}

public sealed class WorkflowBackgroundJobIndex : MapIndex
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

public sealed class WorkflowBackgroundJobIndexProvider : IndexProvider<WorkflowBackgroundJob>
{
    public override void Describe(DescribeContext<WorkflowBackgroundJob> context) =>
        context.For<WorkflowBackgroundJobIndex>().Map(job => new WorkflowBackgroundJobIndex
        {
            JobId = job.JobId,
            Status = job.Status,
            WorkflowInstanceId = job.WorkflowInstanceId,
            CreatedUtc = job.CreatedUtc,
        });
}

public sealed class WorkflowBackgroundJobMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<WorkflowBackgroundJobIndex>(table => table
            .Column<string>(nameof(WorkflowBackgroundJobIndex.JobId), c => c.NotNull().WithLength(64))
            .Column<string>(nameof(WorkflowBackgroundJobIndex.Status), c => c.NotNull().WithLength(16))
            .Column<string>(nameof(WorkflowBackgroundJobIndex.WorkflowInstanceId), c => c.NotNull().WithLength(64))
            .Column<DateTime>(nameof(WorkflowBackgroundJobIndex.CreatedUtc)));

        await SchemaBuilder.AlterIndexTableAsync<WorkflowBackgroundJobIndex>(table =>
        {
            table.CreateIndex("IDX_WorkflowBackgroundJobIndex_JobId", nameof(WorkflowBackgroundJobIndex.JobId));
            table.CreateIndex("IDX_WorkflowBackgroundJobIndex_Status", nameof(WorkflowBackgroundJobIndex.Status), nameof(WorkflowBackgroundJobIndex.CreatedUtc));
        });

        return 1;
    }
}

/// <summary>The job the current scope is running an activity for, when it is: the activity reads its idempotency key here.</summary>
public sealed class BackgroundJobContext
{
    public WorkflowBackgroundJob? Current { get; set; }
}

/// <summary>The jobs of the current unit: one scoped session.</summary>
public sealed class BackgroundJobStore(ISession session)
{
    public Task<WorkflowBackgroundJob?> FindAsync(string jobId) =>
        session.Query<WorkflowBackgroundJob, WorkflowBackgroundJobIndex>(i => i.JobId == jobId).FirstOrDefaultAsync();

    public Task SaveAsync(WorkflowBackgroundJob job) => session.SaveAsync(job);

    public async Task<IReadOnlyList<WorkflowBackgroundJob>> ListUnfinishedAsync() =>
        (await session.Query<WorkflowBackgroundJob, WorkflowBackgroundJobIndex>(i => i.Status == BackgroundJobStatuses.Scheduled || i.Status == BackgroundJobStatuses.Running).OrderBy(i => i.CreatedUtc).ListAsync()).ToList();
}

/// <summary>
/// Replaces the engine's in-memory background-activity scheduler (docs/workflows.md ›
/// Posting on workflows: external calls are boundaries). An activity marked
/// <c>RunAsynchronously</c> - connectors, the external stock tasks - is bookmarked by the
/// engine's middleware and handed here: <see cref="CreateAsync"/> writes the job in the
/// current unit, <see cref="ScheduleAsync(string, CancellationToken)"/> (called by the
/// engine's deferred task when its state is saved) queues the run for after the unit
/// commits. The run is its own unit: a child shell scope executes the activity through the
/// engine's invoker and <see cref="ShellScopedBackgroundActivityInvoker"/> resumes the flow
/// in another. A job whose unit fails is never run; jobs a dead process left are run again
/// at activation. Registered as the engine expects (a singleton of the tenant container):
/// the unit's services are taken from the current shell scope at call time.
/// </summary>
public sealed class DurableBackgroundActivityScheduler(IShellHost shellHost, ShellSettings shellSettings, ILogger<DurableBackgroundActivityScheduler> logger) : IBackgroundActivityScheduler
{
    public async Task<string> CreateAsync(ScheduledBackgroundActivity scheduledBackgroundActivity, CancellationToken cancellationToken = default)
    {
        var job = new WorkflowBackgroundJob
        {
            JobId = Guid.NewGuid().ToString("n"),
            WorkflowInstanceId = scheduledBackgroundActivity.WorkflowInstanceId,
            ActivityNodeId = scheduledBackgroundActivity.ActivityNodeId,
            BookmarkId = scheduledBackgroundActivity.BookmarkId,
            CreatedUtc = DateTime.UtcNow,
        };
        job.IdempotencyKey = $"{job.WorkflowInstanceId}:{job.ActivityNodeId}:{job.BookmarkId}";
        await Store().SaveAsync(job);
        return job.JobId;
    }

    public async Task ScheduleAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var store = Store();
        var job = await store.FindAsync(jobId);
        if (job is null)
        {
            return;
        }

        job.Status = BackgroundJobStatuses.Scheduled;
        await store.SaveAsync(job);

        if (ShellScope.Current is not null && ShellScope.Services.GetRequiredService<WorkflowAfterCommit>().Enqueue($"background job {jobId}", () => RunAsync(jobId)))
        {
            return;
        }

        // No shell scope to wait for (an engine entry point not yet shell-scoped): run in one of
        // our own. The caller's session commits on dispose, so the bookmark may trail the run;
        // the invoker then falls back to the bookmark queue, which tolerates that.
        logger.LogWarning("Background job {Job} scheduled outside a shell scope; running in a new one.", jobId);
        var scope = await shellHost.GetScopeAsync(shellSettings);
        await scope.UsingAsync(_ => RunAsync(jobId));
    }

    public async Task<string> ScheduleAsync(ScheduledBackgroundActivity scheduledBackgroundActivity, CancellationToken cancellationToken = default)
    {
        var jobId = await CreateAsync(scheduledBackgroundActivity, cancellationToken);
        await ScheduleAsync(jobId, cancellationToken);
        return jobId;
    }

    public Task UnscheduledAsync(string jobId, CancellationToken cancellationToken = default) => CancelAsync(jobId, cancellationToken);

    public async Task CancelAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var store = Store();
        var job = await store.FindAsync(jobId);
        if (job is not null && job.Status is BackgroundJobStatuses.Created or BackgroundJobStatuses.Scheduled)
        {
            job.Status = BackgroundJobStatuses.Cancelled;
            job.CompletedUtc = DateTime.UtcNow;
            await store.SaveAsync(job);
        }
    }

    /// <summary>
    /// Runs one job as three sibling child scopes, never nested: mark Running (so a crash
    /// leaves a recoverable record), execute the activity - the only writes in that scope are
    /// the activity's own, and the hand-back to the flow is deferred to after it commits - and
    /// record the outcome. Nested write scopes would hold two transactions at once, which a
    /// single-writer database (SQLite in dev) answers with a lock timeout.
    /// </summary>
    public static async Task RunAsync(string jobId)
    {
        var started = false;
        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var store = scope.ServiceProvider.GetRequiredService<BackgroundJobStore>();
            var job = await store.FindAsync(jobId);
            if (job is null || job.Status is not (BackgroundJobStatuses.Scheduled or BackgroundJobStatuses.Running))
            {
                return;
            }

            job.Attempts++;
            job.Status = BackgroundJobStatuses.Running;
            job.StartedUtc = DateTime.UtcNow;
            await store.SaveAsync(job);
            started = true;
        }, activateShell: false);

        if (!started)
        {
            return;
        }

        Exception? failure = null;
        try
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                var job = (await scope.ServiceProvider.GetRequiredService<BackgroundJobStore>().FindAsync(jobId))!;
                scope.ServiceProvider.GetRequiredService<BackgroundJobContext>().Current = job;
                await scope.ServiceProvider.GetRequiredService<IBackgroundActivityInvoker>().ExecuteAsync(new ScheduledBackgroundActivity(job.WorkflowInstanceId, job.ActivityNodeId, job.BookmarkId));
            }, activateShell: false);
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var store = scope.ServiceProvider.GetRequiredService<BackgroundJobStore>();
            var job = await store.FindAsync(jobId);
            if (job is null || job.Status != BackgroundJobStatuses.Running)
            {
                return;
            }

            job.CompletedUtc = DateTime.UtcNow;
            if (failure is null)
            {
                job.Status = BackgroundJobStatuses.Done;
            }
            else
            {
                job.Status = BackgroundJobStatuses.Failed;
                job.Error = failure.Message;
                scope.ServiceProvider.GetRequiredService<ILogger<DurableBackgroundActivityScheduler>>().LogError(failure, "Background job {Job} for instance {Instance} node {Node} failed.", job.JobId, job.WorkflowInstanceId, job.ActivityNodeId);
            }

            await store.SaveAsync(job);
        }, activateShell: false);
    }

    /// <summary>Jobs a previous process scheduled or started but never finished: run again, in order. Called when the tenant activates, inside a shell scope.</summary>
    public async Task<int> RecoverAsync()
    {
        var pending = await Store().ListUnfinishedAsync();
        foreach (var job in pending)
        {
            logger.LogInformation("Recovering background job {Job} of instance {Instance}.", job.JobId, job.WorkflowInstanceId);
            await RunAsync(job.JobId);
        }

        return pending.Count;
    }

    private static BackgroundJobStore Store() =>
        (ShellScope.Services ?? throw new InvalidOperationException("A background activity can only be scheduled from inside a shell scope: every engine entry point runs in one (requests, scheduled tasks, the background consumers)."))
        .GetRequiredService<BackgroundJobStore>();
}

/// <summary>
/// The engine's background-activity invoker with the hand-back done directly: the bookmark
/// was committed before the job ran, so the flow is resumed by bookmark id in a child shell
/// scope - the next unit - once the activity's own scope has committed, instead of through
/// the bookmark queue and its worker.
/// </summary>
public sealed class ShellScopedBackgroundActivityInvoker(
    IBookmarkQueue bookmarkQueue,
    IWorkflowInstanceManager workflowInstanceManager,
    IWorkflowDefinitionService workflowDefinitionService,
    IVariablePersistenceManager variablePersistenceManager,
    IActivityInvoker activityInvoker,
    IActivityPropertyLogPersistenceEvaluator activityPropertyLogPersistenceEvaluator,
    WorkflowHeartbeatGeneratorFactory workflowHeartbeatGeneratorFactory,
    IServiceProvider serviceProvider,
    ILogger<BackgroundActivityInvoker> logger)
    : BackgroundActivityInvoker(bookmarkQueue, workflowInstanceManager, workflowDefinitionService, variablePersistenceManager, activityInvoker, activityPropertyLogPersistenceEvaluator, workflowHeartbeatGeneratorFactory, serviceProvider, logger)
{
    protected override async Task ResumeWorkflowAsync(ActivityExecutionContext activityExecutionContext, ScheduledBackgroundActivity scheduledBackgroundActivity)
    {
        if (activityExecutionContext.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        var options = await BuildResumeOptionsAsync(activityExecutionContext, scheduledBackgroundActivity);
        var request = new ResumeBookmarkRequest
        {
            WorkflowInstanceId = scheduledBackgroundActivity.WorkflowInstanceId,
            BookmarkId = scheduledBackgroundActivity.BookmarkId,
            Properties = options.Properties,
        };

        // After this scope commits, in a sibling child scope: the activity's writes are in,
        // and no two write transactions are open at once.
        var queued = ShellScope.Current is not null && ShellScope.Services.GetRequiredService<WorkflowAfterCommit>().Enqueue(
            $"resume {request.WorkflowInstanceId} at bookmark {request.BookmarkId}",
            () => ShellScope.UsingChildScopeAsync(scope => scope.ServiceProvider.GetRequiredService<IBookmarkResumer>().ResumeAsync(request), activateShell: false));

        if (!queued)
        {
            await base.ResumeWorkflowAsync(activityExecutionContext, scheduledBackgroundActivity);
        }
    }
}
