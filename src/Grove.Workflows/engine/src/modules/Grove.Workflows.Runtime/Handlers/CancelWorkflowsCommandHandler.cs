using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Mediator.Models;
using Grove.Workflows.Runtime.Commands;
using JetBrains.Annotations;

namespace Grove.Workflows.Runtime.Handlers;

/// <summary>
/// Handles the <see cref="CancelWorkflowsCommand"/>.
/// </summary>
[UsedImplicitly]
public class CancelWorkflowsCommandHandler(IWorkflowRuntime workflowRuntime) : ICommandHandler<CancelWorkflowsCommand>
{
    /// <inheritdoc />
    public async Task<Unit> HandleAsync(CancelWorkflowsCommand command, CancellationToken cancellationToken)
    {
        var workflowInstanceId = command.Request.WorkflowInstanceId;
        var workflowClient = await workflowRuntime.CreateClientAsync(workflowInstanceId, cancellationToken);
        await workflowClient.CancelAsync(cancellationToken);
        
        return Unit.Instance;
    }
}