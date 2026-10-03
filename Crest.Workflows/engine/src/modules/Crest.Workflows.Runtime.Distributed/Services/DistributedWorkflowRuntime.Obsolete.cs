using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.Matches;
using Crest.Workflows.Runtime.Options;
using Crest.Workflows.Runtime.Parameters;
using Crest.Workflows.Runtime.Params;
using Crest.Workflows.Runtime.Requests;
using Crest.Workflows.Runtime.Results;
using Crest.Workflows.State;

namespace Crest.Workflows.Runtime.Distributed;

public partial class DistributedWorkflowRuntime
{
    private readonly Lazy<ObsoleteWorkflowRuntime> _obsoleteApi;
    private ObsoleteWorkflowRuntime ObsoleteApi => _obsoleteApi.Value;

    public Task<CanStartWorkflowResult> CanStartWorkflowAsync(string definitionId, StartWorkflowRuntimeParams? options = null) => ObsoleteApi.CanStartWorkflowAsync(definitionId, options);
    public Task<WorkflowExecutionResult> StartWorkflowAsync(string definitionId, StartWorkflowRuntimeParams? options = null) => ObsoleteApi.StartWorkflowAsync(definitionId, options);
    public Task<ICollection<WorkflowExecutionResult>> StartWorkflowsAsync(string activityTypeName, object bookmarkPayload, TriggerWorkflowsOptions? options = null) => ObsoleteApi.StartWorkflowsAsync(activityTypeName, bookmarkPayload, options);
    public Task<WorkflowExecutionResult?> TryStartWorkflowAsync(string definitionId, StartWorkflowRuntimeParams? options = null) => ObsoleteApi.TryStartWorkflowAsync(definitionId, options);
    public Task<WorkflowExecutionResult?> ResumeWorkflowAsync(string workflowInstanceId, ResumeWorkflowRuntimeParams? options = null) => ObsoleteApi.ResumeWorkflowAsync(workflowInstanceId, options);
    public Task<ICollection<WorkflowExecutionResult>> ResumeWorkflowsAsync(string activityTypeName, object bookmarkPayload, TriggerWorkflowsOptions? options = null) => ObsoleteApi.ResumeWorkflowsAsync(activityTypeName, bookmarkPayload, options);
    public Task<TriggerWorkflowsResult> TriggerWorkflowsAsync(string activityTypeName, object bookmarkPayload, TriggerWorkflowsOptions? options = null) => ObsoleteApi.TriggerWorkflowsAsync(activityTypeName, bookmarkPayload, options);
    public Task<WorkflowExecutionResult> ExecuteWorkflowAsync(WorkflowMatch match, ExecuteWorkflowParams? options = null) => ObsoleteApi.ExecuteWorkflowAsync(match, options);
    public Task<CancellationResult> CancelWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default) => ObsoleteApi.CancelWorkflowAsync(workflowInstanceId, cancellationToken);
    public Task<IEnumerable<WorkflowMatch>> FindWorkflowsAsync(WorkflowsFilter filter, CancellationToken cancellationToken = default) => ObsoleteApi.FindWorkflowsAsync(filter, cancellationToken);
    public Task<WorkflowState?> ExportWorkflowStateAsync(string workflowInstanceId, CancellationToken cancellationToken = default) => ObsoleteApi.ExportWorkflowStateAsync(workflowInstanceId, cancellationToken);
    public Task ImportWorkflowStateAsync(WorkflowState workflowState, CancellationToken cancellationToken = default) => ObsoleteApi.ImportWorkflowStateAsync(workflowState, cancellationToken);
    public Task UpdateBookmarkAsync(StoredBookmark bookmark, CancellationToken cancellationToken = default) => ObsoleteApi.UpdateBookmarkAsync(bookmark, cancellationToken);
    public Task<long> CountRunningWorkflowsAsync(CountRunningWorkflowsRequest request, CancellationToken cancellationToken = default) => ObsoleteApi.CountRunningWorkflowsAsync(request, cancellationToken);
}