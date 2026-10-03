using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Models;
using Crest.Workflows.Pipelines.WorkflowExecution;
using Crest.Workflows.Runtime.Middleware.Workflows;
using Crest.Workflows.Runtime.Notifications;
using Crest.Workflows.State;

namespace Crest.Workflows.Runtime;

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