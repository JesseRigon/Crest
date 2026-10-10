using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Exceptions;
using Crest.Workflows.Runtime.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crest.Workflows.Scheduling.Tasks;

/// <summary>
/// A task that resumes a workflow.
/// </summary>
public class ResumeWorkflowTask(ScheduleExistingWorkflowInstanceRequest request) : ITask
{
    /// <summary>
    /// The request this task will run: the instance to resume. A host reads it to decide who
    /// the run acts as before the engine starts.
    /// </summary>
    public ScheduleExistingWorkflowInstanceRequest Request => request;

    /// <inheritdoc />
    public async ValueTask ExecuteAsync(TaskExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var workflowRuntime = context.ServiceProvider.GetRequiredService<IWorkflowRuntime>();
        var logger = context.ServiceProvider.GetRequiredService<ILogger<ResumeWorkflowTask>>();

        try
        {
            var workflowClient = await workflowRuntime.CreateClientAsync(request.WorkflowInstanceId, cancellationToken);
            var runWorkflowInstanceRequest = new RunWorkflowInstanceRequest
            {
                Input = request.Input,
                Properties = request.Properties,
                ActivityHandle = request.ActivityHandle,
                BookmarkId = request.BookmarkId
            };
            await workflowClient.RunInstanceAsync(runWorkflowInstanceRequest, cancellationToken);
        }
        catch (WorkflowInstanceNotFoundException ex)
        {
            logger.LogWarning(
                "Scheduled workflow instance {WorkflowInstanceId} no longer exists and was likely deleted. Skipping execution.",
                ex.InstanceId);
        }
    }
}