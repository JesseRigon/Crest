using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Mediator.Models;
using Crest.Workflows.Runtime.Commands;
using JetBrains.Annotations;

namespace Crest.Workflows.Runtime.Handlers;

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