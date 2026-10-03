using Crest.Workflows.Activities;
using Crest.Workflows.Models;
using Crest.Workflows.Options;
using Crest.Workflows.State;

namespace Crest.Workflows;

/// <summary>
/// Runs a given workflow by scheduling its root activity.
/// </summary>
public interface IWorkflowRunner
{
    Task<RunWorkflowResult> RunAsync(IActivity activity, RunWorkflowOptions? options = null, CancellationToken cancellationToken = default);
    Task<RunWorkflowResult> RunAsync(IWorkflow workflow, RunWorkflowOptions? options = null, CancellationToken cancellationToken = default);
    Task<RunWorkflowResult<TResult>> RunAsync<TResult>(WorkflowBase<TResult> workflow, RunWorkflowOptions? options = null, CancellationToken cancellationToken = default);
    Task<RunWorkflowResult> RunAsync<T>(RunWorkflowOptions? options = null, CancellationToken cancellationToken = default) where T : IWorkflow, new();
    Task<TResult> RunAsync<T, TResult>(RunWorkflowOptions? options = null, CancellationToken cancellationToken = default) where T : WorkflowBase<TResult>, new();
    Task<RunWorkflowResult> RunAsync(Workflow workflow, RunWorkflowOptions? options = null, CancellationToken cancellationToken = default);
    Task<RunWorkflowResult> RunAsync(Workflow workflow, WorkflowState workflowState, RunWorkflowOptions? options = null, CancellationToken cancellationToken = default);
    Task<RunWorkflowResult> RunAsync(WorkflowGraph workflowGraph, RunWorkflowOptions? options = null, CancellationToken cancellationToken = default);
    Task<RunWorkflowResult> RunAsync(WorkflowGraph workflowGraph, WorkflowState workflowState, RunWorkflowOptions? options = null, CancellationToken cancellationToken = default);
    Task<RunWorkflowResult> RunAsync(WorkflowExecutionContext workflowExecutionContext);
}