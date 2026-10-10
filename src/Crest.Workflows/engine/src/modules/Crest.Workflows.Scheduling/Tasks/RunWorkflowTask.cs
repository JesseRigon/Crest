using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Scheduling.Tasks;

/// <summary>
/// A task that runs a workflow.
/// </summary>
public class RunWorkflowTask : ITask
{
    private readonly ScheduleNewWorkflowInstanceRequest _request;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunWorkflowTask"/> class.
    /// </summary>
    public RunWorkflowTask(ScheduleNewWorkflowInstanceRequest request)
    {
        _request = request;
    }

    /// <summary>
    /// The request this task will run: the definition and the input. A host reads it to decide
    /// who the run acts as before the engine starts.
    /// </summary>
    public ScheduleNewWorkflowInstanceRequest Request => _request;
    
    /// <inheritdoc />
    public async ValueTask ExecuteAsync(TaskExecutionContext context)
    {
        var workflowRuntime = context.ServiceProvider.GetRequiredService<IWorkflowRuntime>();
        var workflowClient = await workflowRuntime.CreateClientAsync();
        var request = new CreateAndRunWorkflowInstanceRequest
        {
            WorkflowDefinitionHandle = _request.WorkflowDefinitionHandle,
            TriggerActivityId = _request.TriggerActivityId,
            Input = _request.Input,
            Properties = _request.Properties,
            ParentId = _request.ParentId,
            CorrelationId = _request.CorrelationId
        };
        await workflowClient.CreateAndRunInstanceAsync(request);
    }
}