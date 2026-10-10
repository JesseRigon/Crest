using System.Text.Json;
using Crest.Workflows.Management;
using Crest.Workflows.Runtime.Middleware.Activities;
using Crest.Workflows.Runtime.Options;
using Microsoft.Extensions.Logging;

namespace Crest.Workflows.Runtime;

/// <summary>
/// An activity invoker that invokes activities detached from the workflow. This is useful for invoking activities from a background worker.
/// </summary>
public class BackgroundActivityInvoker(
    IBookmarkQueue bookmarkQueue,
    IWorkflowInstanceManager workflowInstanceManager,
    IWorkflowDefinitionService workflowDefinitionService,
    IVariablePersistenceManager variablePersistenceManager,
    IActivityInvoker activityInvoker,
    IActivityPropertyLogPersistenceEvaluator activityPropertyLogPersistenceEvaluator,
    WorkflowHeartbeatGeneratorFactory workflowHeartbeatGeneratorFactory,
    IServiceProvider serviceProvider,
    ILogger<BackgroundActivityInvoker> logger)
    : IBackgroundActivityInvoker
{
    private readonly ILogger _logger = logger;

    /// <inheritdoc />
    public async Task ExecuteAsync(ScheduledBackgroundActivity scheduledBackgroundActivity, CancellationToken cancellationToken = default)
    {
        var workflowInstanceId = scheduledBackgroundActivity.WorkflowInstanceId;
        var workflowInstance = await workflowInstanceManager.FindByIdAsync(workflowInstanceId, cancellationToken);
        if (workflowInstance == null) throw new("Workflow instance not found");
        var workflowState = workflowInstance.WorkflowState;
        var workflow = await workflowDefinitionService.FindWorkflowGraphAsync(workflowInstance.DefinitionVersionId, cancellationToken);
        if (workflow == null) throw new("Workflow definition not found");
        var workflowExecutionContext = await WorkflowExecutionContext.CreateAsync(serviceProvider, workflow, workflowState, cancellationToken: cancellationToken);
        var activityNodeId = scheduledBackgroundActivity.ActivityNodeId;
        var activityExecutionContext = workflowExecutionContext.ActivityExecutionContexts.First(x => x.NodeId == activityNodeId);

        using (workflowHeartbeatGeneratorFactory.CreateHeartbeatGenerator(workflowExecutionContext))
        {
            await variablePersistenceManager.LoadVariablesAsync(workflowExecutionContext);
            activityExecutionContext.SetIsBackgroundExecution();
            await activityInvoker.InvokeAsync(activityExecutionContext);
            await variablePersistenceManager.SaveVariablesAsync(workflowExecutionContext);
        }
        await ResumeWorkflowAsync(activityExecutionContext, scheduledBackgroundActivity);
    }

    /// <summary>
    /// Hands the finished activity's results back to its workflow. Crest: virtual so a host
    /// can resume the bookmark directly in a transaction of its own instead of through the
    /// bookmark queue (the queue exists for the race where the job outruns the bookmark's
    /// persistence, which a host that schedules after commit never has).
    /// </summary>
    protected virtual async Task ResumeWorkflowAsync(ActivityExecutionContext activityExecutionContext, ScheduledBackgroundActivity scheduledBackgroundActivity)
    {
        var cancellationToken = activityExecutionContext.CancellationToken;
        var activityNodeId = scheduledBackgroundActivity.ActivityNodeId;
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Background execution for activity {ActivityNodeId} was canceled", activityNodeId);
            return;
        }

        var resumeBookmarkOptions = await BuildResumeOptionsAsync(activityExecutionContext, scheduledBackgroundActivity);
        var enqueuedBookmark = new NewBookmarkQueueItem
        {
            WorkflowInstanceId = scheduledBackgroundActivity.WorkflowInstanceId,
            BookmarkId = scheduledBackgroundActivity.BookmarkId,
            Options = resumeBookmarkOptions
        };
        await bookmarkQueue.EnqueueAsync(enqueuedBookmark, cancellationToken);
    }

    /// <summary>The captured output, outcomes, journal, bookmarks and properties the resumed activity reads back.</summary>
    protected async Task<ResumeBookmarkOptions> BuildResumeOptionsAsync(ActivityExecutionContext activityExecutionContext, ScheduledBackgroundActivity scheduledBackgroundActivity)
    {
        var activityNodeId = scheduledBackgroundActivity.ActivityNodeId;

        var inputKey = BackgroundActivityInvokerMiddleware.GetBackgroundActivityOutputKey(activityNodeId);
        var outcomesKey = BackgroundActivityInvokerMiddleware.GetBackgroundActivityOutcomesKey(activityNodeId);
        var completedKey = BackgroundActivityInvokerMiddleware.GetBackgroundActivityCompletedKey(activityNodeId);
        var journalDataKey = BackgroundActivityInvokerMiddleware.GetBackgroundActivityJournalDataKey(activityNodeId);
        var bookmarksKey = BackgroundActivityInvokerMiddleware.GetBackgroundActivityBookmarksKey(activityNodeId);
        var scheduledActivitiesKey = BackgroundActivityInvokerMiddleware.GetBackgroundActivityScheduledActivitiesKey(activityNodeId);
        var propsKey = BackgroundActivityInvokerMiddleware.GetBackgroundActivityPropertiesKey(activityNodeId);
        var outcomes = activityExecutionContext.GetBackgroundOutcomes()?.ToList();
        var completed = activityExecutionContext.GetBackgroundCompleted();
        var scheduledActivities = activityExecutionContext.GetBackgroundScheduledActivities().ToList();
        var outputValues = await activityPropertyLogPersistenceEvaluator.GetPersistableOutputAsync(activityExecutionContext);
        var bookmarkProps = new Dictionary<string, object>
        {
            [scheduledActivitiesKey] = JsonSerializer.Serialize(scheduledActivities),
            [inputKey] = outputValues,
            [journalDataKey] = activityExecutionContext.JournalData,
            [bookmarksKey] = activityExecutionContext.Bookmarks.ToList(),
            [propsKey] = activityExecutionContext.Properties.ToDictionary() // ChangeTrackingDictionary is not persistable, so we need to create a copy of the dictionary.
        };

        if (outcomes != null) bookmarkProps[outcomesKey] = outcomes;
        if (completed != null) bookmarkProps[completedKey] = completed;

        return new ResumeBookmarkOptions
        {
            Properties = bookmarkProps,
        };
    }
}