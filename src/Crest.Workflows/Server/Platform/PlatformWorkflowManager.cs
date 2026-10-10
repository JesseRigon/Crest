using System.Text.Json.Nodes;
using Crest.Workflows.Contexts;
using Crest.Workflows.Runtime;
using Microsoft.Extensions.Logging;
using Crest.Workflows.Platform.Abstractions.Models;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Models;
using Crest.Workflows.Platform.Services;
// The engine and the stock module both define StockWorkflowExecutionContext, StockWorkflowStatus and
// StockActivity; the stock ones are meant everywhere in this file.
using StockActivity = Crest.Workflows.Platform.Activities.IActivity;
using StockWorkflowExecutionContext = Crest.Workflows.Platform.Models.WorkflowExecutionContext;
using StockWorkflowStatus = Crest.Workflows.Platform.Models.WorkflowStatus;

namespace Crest.Workflows.Platform;

/// <summary>
/// The tenant's <see cref="IWorkflowManager"/>, registered after the stock one so upstream
/// modules (Contents, Users, Email, ...) reach the engine without knowing it changed:
/// <c>TriggerEventAsync(name, input, correlationId)</c> becomes an <see cref="PlatformEvent"/>
/// stimulus carrying the stock input and the acting user. Starting, resuming or executing
/// stock workflow types is not supported — definitions live in the engine now — and says so.
/// </summary>
public sealed class PlatformWorkflowManager(
    Units.WorkflowStimulusQueue queue,
    IWorkflowUserContextAccessor userContext,
    StockActivityRunner runner,
    ILogger<PlatformWorkflowManager> logger) : IWorkflowManager
{
    public async Task<IEnumerable<StockWorkflowExecutionContext>> TriggerEventAsync(string name, IDictionary<string, object>? input = null, string? correlationId = null, bool isExclusive = false, bool isAlwaysCorrelated = false)
    {
        // Workflow definitions are content items too: publishing one must not fan out to
        // content triggers (the stock Contents handler fires for every content type).
        if (input is not null && input.TryGetValue("ContentEvent", out var contentEvent) && contentEvent is Crest.ContentManagement.Workflows.ContentEventContext { ContentType: "WorkflowDefinition" })
        {
            return [];
        }

        var workflowInput = new Dictionary<string, object>(input ?? new Dictionary<string, object>())
        {
            [WorkflowsConstants.InputKeys.Actor] = userContext.Capture(),
        };

        // Fires after the current unit commits (docs/workflows.md › Posting on workflows).
        await queue.EnqueueAsync<PlatformEvent>(new PlatformEventStimulus(name), new() { CorrelationId = correlationId, Input = workflowInput });
        logger.LogDebug("Stock event {Event} queued for {Correlation}; it fires after commit.", name, correlationId);
        return [];
    }

    public Workflow NewWorkflow(WorkflowType workflowType, string? correlationId = null) =>
        new() { WorkflowTypeId = workflowType.WorkflowTypeId, WorkflowId = Guid.NewGuid().ToString("n"), CorrelationId = correlationId, State = [], Status = StockWorkflowStatus.Idle };

    public Task<StockWorkflowExecutionContext> CreateWorkflowExecutionContextAsync(WorkflowType workflowType, Workflow workflow, IDictionary<string, object>? input = null)
    {
        var activities = workflowType.Activities.Select(record => new ActivityContext { ActivityRecord = record, Activity = runner.Instantiate(record.Name, record.Properties.ToJsonString()) }).ToList();
        return Task.FromResult(new StockWorkflowExecutionContext(workflowType, workflow, StockActivityRunner.ToStockInput(input ?? new Dictionary<string, object>()), new Dictionary<string, object>(), new Dictionary<string, object>(), [], null, activities));
    }

    public Task<ActivityContext> CreateActivityExecutionContextAsync(ActivityRecord activityRecord, JsonObject properties) =>
        Task.FromResult(new ActivityContext { ActivityRecord = activityRecord, Activity = runner.Instantiate(activityRecord.Name, properties.ToJsonString()) });

    public Task<StockWorkflowExecutionContext> StartWorkflowAsync(WorkflowType workflowType, ActivityRecord? startActivity = null, IDictionary<string, object>? input = null, string? correlationId = null) => throw NotSupported();
    public Task<StockWorkflowExecutionContext> RestartWorkflowAsync(WorkflowType workflowType, IDictionary<string, object>? input = null, string? correlationId = null) => throw NotSupported();
    public Task<StockWorkflowExecutionContext> ResumeWorkflowAsync(Workflow workflow, BlockingActivity awaitingActivity, IDictionary<string, object>? input = null) => throw NotSupported();
    public Task<IEnumerable<ActivityRecord>> ExecuteWorkflowAsync(StockWorkflowExecutionContext workflowExecutionContext, ActivityRecord activity) => throw NotSupported();

    private static NotSupportedException NotSupported() =>
        new("Stock Crest workflow types are not run in this tenant: workflow definitions live in Crest.Workflows. Use the engine's API (crest-workflows/api) or a Crest trigger.");
}
