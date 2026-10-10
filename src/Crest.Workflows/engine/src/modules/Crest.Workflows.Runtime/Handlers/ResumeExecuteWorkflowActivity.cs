using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Helpers;
using Crest.Workflows.Notifications;
using Crest.Workflows.Runtime.Activities;
using Crest.Workflows.Runtime.Stimuli;
using JetBrains.Annotations;

namespace Crest.Workflows.Runtime.Handlers;

/// <summary>
/// Resumes any blocking <see cref="ExecuteWorkflow"/> activities when its child workflow completes.
/// </summary>
[UsedImplicitly]
internal class ResumeExecuteWorkflowActivity(IBookmarkQueue bookmarkQueue, IStimulusHasher stimulusHasher) : INotificationHandler<WorkflowExecuted>
{
    private static readonly string ActivityTypeName = ActivityTypeNameHelper.GenerateTypeName<ExecuteWorkflow>();

    public async Task HandleAsync(WorkflowExecuted notification, CancellationToken cancellationToken)
    {
        var workflowState = notification.WorkflowState;

        if (workflowState.Status != WorkflowStatus.Finished)
            return;

        var props = workflowState.Properties;
        var waitForCompletion = props.TryGetValue("WaitForCompletion", out var waitForCompletionValue) && (bool)waitForCompletionValue;

        if (!waitForCompletion)
            return;

        var parentInstanceId = (string)props["ParentInstanceId"];
        var stimulus = new ExecuteWorkflowStimulus(notification.WorkflowState.Id);
        var input = workflowState.Output;

        var bookmarkQueueItem = new NewBookmarkQueueItem
        {
            WorkflowInstanceId = parentInstanceId,
            ActivityTypeName = ActivityTypeName,
            StimulusHash = stimulusHasher.Hash(ActivityTypeName, stimulus),
            Options = new()
            {
                Input = input
            }
        };

        await bookmarkQueue.EnqueueAsync(bookmarkQueueItem, cancellationToken);
    }
}