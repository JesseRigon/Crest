using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Helpers;
using Crest.Workflows.Notifications;
using Crest.Workflows.Runtime.Activities;
using Crest.Workflows.Runtime.Options;
using Crest.Workflows.Runtime.Stimuli;
using JetBrains.Annotations;

namespace Crest.Workflows.Runtime.Handlers;

/// <summary>
/// Resumes any blocking <see cref="BulkDispatchWorkflows"/> activities when its child workflows complete.
/// </summary>
[PublicAPI]
internal class ResumeBulkDispatchWorkflowActivity(IBookmarkQueue bookmarkQueue, IStimulusHasher stimulusHasher) : INotificationHandler<WorkflowExecuted>
{
    public async Task HandleAsync(WorkflowExecuted notification, CancellationToken cancellationToken)
    {
        var workflowState = notification.WorkflowState;

        if (workflowState.Status != WorkflowStatus.Finished)
            return;

        var waitForCompletion = workflowState.Properties.TryGetValue("WaitForCompletion", out var waitForCompletionValue) && (bool)waitForCompletionValue;
        
        if (!waitForCompletion)
            return;
        
        if (!workflowState.Properties.TryGetValue("ParentInstanceId", out var parentInstanceIdValue))
            return;
        
        var parentInstanceId = (string)parentInstanceIdValue;
        var activityTypeName = ActivityTypeNameHelper.GenerateTypeName<BulkDispatchWorkflows>();
        var stimulus = new BulkDispatchWorkflowsStimulus(parentInstanceId);
        var stimulusHash = stimulusHasher.Hash(activityTypeName, stimulus);
        var workflowInstanceId = workflowState.Id;
        var input = new Dictionary<string, object>
        {
            ["WorkflowOutput"] = workflowState.Output,
            ["WorkflowInstanceId"] = workflowInstanceId,
            ["WorkflowStatus"] = workflowState.Status,
            ["WorkflowSubStatus"] = workflowState.SubStatus,
        };

        var resumeBookmarkOptions = new ResumeBookmarkOptions
        {
            Input = input
        };
        var bookmarkQueueItem = new NewBookmarkQueueItem
        {
            WorkflowInstanceId = parentInstanceId,
            ActivityTypeName = activityTypeName,
            StimulusHash = stimulusHash,
            Options = resumeBookmarkOptions
        };
        await bookmarkQueue.EnqueueAsync(bookmarkQueueItem, cancellationToken);
    }
}