using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Models;
using Grove.Workflows.Pipelines.WorkflowExecution;
using Grove.Workflows.Runtime.Middleware.Workflows;
using Grove.Workflows.Runtime.Notifications;
using Grove.Workflows.State;

namespace Grove.Workflows.Runtime;

/// <inheritdoc />
public class WorkflowCanceler(
    IWorkflowExecutionPipeline workflowExecutionPipeline, 
    IWorkflowStateExtractor workflowStateExtractor, 
    IMediator mediator, 
    IServiceProvider serviceProvider) : IWorkflowCanceler
{
    /// <inheritdoc />
    public async Task<WorkflowState> CancelWorkflowAsync(WorkflowGraph workflowGraph, WorkflowState workflowState, CancellationToken cancellationToken = default)
    {
        var workflowExecutionContext = await WorkflowExecutionContext.CreateAsync(serviceProvider, workflowGraph, workflowState, cancellationToken: cancellationToken);
        await CancelWorkflowAsync(workflowExecutionContext, cancellationToken);
        return workflowStateExtractor.Extract(workflowExecutionContext);
    }

    /// <inheritdoc />
    public async Task CancelWorkflowAsync(WorkflowExecutionContext workflowExecutionContext, CancellationToken cancellationToken = default)
    {
        await mediator.SendAsync(new WorkflowCancelling(workflowExecutionContext.Id), cancellationToken);
        var pipelineBuilder = new WorkflowExecutionPipelineBuilder(serviceProvider);
        workflowExecutionPipeline.ConfigurePipelineBuilder(pipelineBuilder);
        pipelineBuilder.ReplaceTerminal<CancelWorkflowMiddleware>();
        var pipeline = pipelineBuilder.Build();
        await pipeline(workflowExecutionContext);
        await mediator.SendAsync(new WorkflowCancelled(workflowExecutionContext.Id), cancellationToken);
    }
}