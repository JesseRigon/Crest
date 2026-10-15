using Grove.Workflows.Api.Models;
using Grove.Workflows.Common.Entities;
using Grove.Workflows.Common.Models;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Entities;
using Grove.Workflows.Management.Filters;
using Grove.Workflows.Management.Models;
using Grove.Workflows.Models;
using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Filters;
using Grove.Workflows.Runtime.OrderDefinitions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Open.Linq.AsyncExtensions;

namespace Grove.Workflows.Api.Endpoints.WorkflowInstances;

/// <summary>
/// The workflow instance operations: the list and the reads, the journal, variables, cancel,
/// delete and the bulk actions. Export and import have their own file.
/// </summary>
public static class WorkflowInstanceEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/workflow-instances", ListFromQueryAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowInstances);
        routes.MapPost("/workflow-instances", ListAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowInstances);
        routes.MapGet("/workflow-instances/{id}", GetAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowInstances);
        routes.MapGet("/workflow-instances/{id}/execution-state", GetExecutionStateAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowInstances);
        routes.MapGet("/workflow-instances/{id}/journal", ListJournalAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowInstances);
        routes.MapPost("/workflow-instances/{id}/journal", ListFilteredJournalAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowInstances);
        routes.MapGet("/workflow-instances/{workflowInstanceId}/journal/{activityId}", GetLastJournalEntryAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowInstances);
        routes.MapGet("/workflow-instances/{id}/variables", ListVariablesAsync).RequireAuthorization(EngineApiPolicies.ReadWorkflowInstances);
        routes.MapPost("/workflow-instances/{id}/variables", SetVariablesAsync).RequireAuthorization(EngineApiPolicies.WriteWorkflowInstances);
        routes.MapDelete("/workflow-instances/{id}", DeleteAsync).RequireAuthorization(EngineApiPolicies.DeleteWorkflowInstances);
        routes.MapPost("/cancel/workflow-instances/{id}", CancelAsync).RequireAuthorization(EngineApiPolicies.CancelWorkflowInstances);
        routes.MapPost("/bulk-actions/cancel/workflow-instances/", BulkCancelAsync).RequireAuthorization(EngineApiPolicies.CancelWorkflowInstances);
        routes.MapPost("/bulk-actions/delete/workflow-instances", BulkDeleteAsync).RequireAuthorization(EngineApiPolicies.DeleteWorkflowInstances);
        routes.MapPost("/bulk-actions/delete/workflow-instances/by-id", BulkDeleteAsync).RequireAuthorization(EngineApiPolicies.DeleteWorkflowInstances);
    }

    private static Task<IResult> ListFromQueryAsync(
        HttpContext context,
        [AsParameters] ListWorkflowInstancesQuery query,
        [FromServices] IWorkflowInstanceStore store,
        CancellationToken cancellationToken) =>
        ListCoreAsync(context, query.ToRequest(), store, cancellationToken);

    private static Task<IResult> ListAsync(
        HttpContext context,
        EngineBody<ListWorkflowInstancesRequest> body,
        [FromServices] IWorkflowInstanceStore store,
        CancellationToken cancellationToken) =>
        ListCoreAsync(context, body.Value, store, cancellationToken);

    private static async Task<IResult> ListCoreAsync(HttpContext context, ListWorkflowInstancesRequest request, IWorkflowInstanceStore store, CancellationToken cancellationToken)
    {
        var filter = new WorkflowInstanceFilter
        {
            IsSystem = request.IsSystem,
            SearchTerm = request.SearchTerm,
            Name = request.Name,
            DefinitionId = request.DefinitionId,
            DefinitionIds = request.DefinitionIds?.Count > 0 ? request.DefinitionIds : null,
            Version = request.Version,
            CorrelationId = request.CorrelationId,
            WorkflowStatus = request.Status,
            WorkflowSubStatus = request.SubStatus,
            WorkflowStatuses = request.Statuses?.Length > 0 ? request.Statuses.Select(s => Enum.Parse<WorkflowStatus>(s, true)).ToList() : null,
            WorkflowSubStatuses = request.SubStatuses?.Length > 0 ? request.SubStatuses.Select(s => Enum.Parse<WorkflowSubStatus>(s, true)).ToList() : null,
            HasIncidents = request.HasIncidents,
            TimestampFilters = request.TimestampFilters?.Count > 0 ? request.TimestampFilters : null,
        };

        var pageArgs = PageArgs.FromPage(request.Page, request.PageSize);
        var orderBy = request.OrderBy ?? OrderByWorkflowInstance.Created;
        var direction = orderBy == OrderByWorkflowInstance.Name ? request.OrderDirection ?? OrderDirection.Ascending : request.OrderDirection ?? OrderDirection.Descending;
        var summaries = orderBy switch
        {
            OrderByWorkflowInstance.UpdatedAt => await store.SummarizeManyAsync(filter, pageArgs, new WorkflowInstanceOrder<DateTimeOffset?> { KeySelector = p => p.UpdatedAt, Direction = direction }, cancellationToken),
            OrderByWorkflowInstance.Finished => await store.SummarizeManyAsync(filter, pageArgs, new WorkflowInstanceOrder<DateTimeOffset?> { KeySelector = p => p.FinishedAt, Direction = direction }, cancellationToken),
            OrderByWorkflowInstance.Name => await store.SummarizeManyAsync(filter, pageArgs, new WorkflowInstanceOrder<string> { KeySelector = p => p.Name!, Direction = direction }, cancellationToken),
            _ => await store.SummarizeManyAsync(filter, pageArgs, new WorkflowInstanceOrder<DateTimeOffset> { KeySelector = p => p.CreatedAt, Direction = direction }, cancellationToken),
        };

        return EngineResults.Json(context, new ListWorkflowInstancesResponse(summaries.Items, summaries.TotalCount));
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        [FromRoute] string id,
        [FromServices] IWorkflowInstanceStore store,
        CancellationToken cancellationToken)
    {
        var workflowInstance = await store.FindAsync(new WorkflowInstanceFilter { Id = id }, cancellationToken);
        return workflowInstance == null ? EngineResults.NotFound() : EngineResults.Json(context, ToModel(workflowInstance));
    }

    private static async Task<IResult> GetExecutionStateAsync(
        HttpContext context,
        [FromRoute] string id,
        [FromServices] IWorkflowInstanceStore store,
        CancellationToken cancellationToken)
    {
        var workflowInstance = await store.FindAsync(new WorkflowInstanceFilter { Id = id }, cancellationToken);
        return workflowInstance == null
            ? EngineResults.NotFound()
            : EngineResults.Json(context, new ExecutionStateResponse(workflowInstance.Status, workflowInstance.SubStatus, workflowInstance.UpdatedAt));
    }

    private static Task<IResult> ListJournalAsync(
        HttpContext context,
        [FromRoute] string id,
        [AsParameters] JournalPageQuery query,
        [FromServices] IWorkflowExecutionLogStore store,
        CancellationToken cancellationToken) =>
        JournalPageAsync(context, id, null, PageArgs.From(query.Page, query.PageSize, query.Skip, query.Take), store, cancellationToken);

    private static Task<IResult> ListFilteredJournalAsync(
        HttpContext context,
        [FromRoute] string id,
        EngineBody<FilteredJournalRequest> body,
        [FromServices] IWorkflowExecutionLogStore store,
        CancellationToken cancellationToken)
    {
        var request = body.Value;
        return JournalPageAsync(context, id, request.Filter, PageArgs.From(request.Page, request.PageSize, request.Skip, request.Take), store, cancellationToken);
    }

    private static async Task<IResult> JournalPageAsync(HttpContext context, string workflowInstanceId, JournalFilter? journalFilter, PageArgs pageArgs, IWorkflowExecutionLogStore store, CancellationToken cancellationToken)
    {
        var filter = new WorkflowExecutionLogRecordFilter
        {
            WorkflowInstanceId = workflowInstanceId,
            ActivityIds = journalFilter?.ActivityIds,
            ActivityNodeIds = journalFilter?.ActivityNodeIds,
            ExcludeActivityTypes = journalFilter?.ExcludedActivityTypes,
            EventNames = journalFilter?.EventNames,
        };

        var order = new WorkflowExecutionLogRecordOrder<long>(x => x.Sequence, OrderDirection.Ascending);
        var page = await store.FindManyAsync(filter, pageArgs, order, cancellationToken);
        var models = page.Items.Select(x => new ExecutionLogRecord(
                x.Id, x.ActivityInstanceId, x.ParentActivityInstanceId, x.ActivityId, x.ActivityType, x.ActivityTypeVersion, x.ActivityName, x.ActivityNodeId,
                x.Timestamp, x.Sequence, x.EventName, x.Message, x.Source, x.ActivityState, x.Payload))
            .ToList();

        return EngineResults.Json(context, new JournalResponse(models, page.TotalCount));
    }

    /// <summary>The last Started, Completed or Faulted entry of an activity.</summary>
    private static async Task<IResult> GetLastJournalEntryAsync(
        HttpContext context,
        [FromRoute] string workflowInstanceId,
        [FromRoute] string activityId,
        [FromServices] IWorkflowExecutionLogStore store,
        CancellationToken cancellationToken)
    {
        var filter = new WorkflowExecutionLogRecordFilter { WorkflowInstanceId = workflowInstanceId, ActivityId = activityId, EventNames = ["Started", "Completed", "Faulted"] };
        var sort = new WorkflowExecutionLogRecordOrder<DateTimeOffset>(x => x.Timestamp, OrderDirection.Descending);
        var entry = await store.FindAsync(filter, sort, cancellationToken);
        return entry == null ? EngineResults.NotFound() : EngineResults.Json(context, entry);
    }

    private static async Task<IResult> ListVariablesAsync(
        HttpContext context,
        [FromRoute] string id,
        [FromServices] IWorkflowInstanceVariableManager variableManager,
        CancellationToken cancellationToken)
    {
        var variables = await variableManager.GetVariablesAsync(id, ["LargeData"], cancellationToken).ToList();
        var models = variables.Select(x => new ResolvedVariableModel(x.Variable.Id, x.Variable.Name, x.Value)).ToList();
        return EngineResults.Json(context, new ListResponse<ResolvedVariableModel>(models));
    }

    private static async Task<IResult> SetVariablesAsync(
        HttpContext context,
        [FromRoute] string id,
        EngineBody<SetVariablesRequest> body,
        [FromServices] IWorkflowInstanceVariableManager variableManager,
        CancellationToken cancellationToken)
    {
        var resolved = await variableManager.SetVariablesAsync(id, body.Value.Variables ?? [], cancellationToken).ToList();
        var models = resolved.Select(x => new ResolvedVariableModel(x.Variable.Id, x.Variable.Name, x.Value)).ToList();
        return EngineResults.Json(context, new ListResponse<ResolvedVariableModel>(models));
    }

    private static async Task<IResult> DeleteAsync(
        [FromRoute] string id,
        [FromServices] IWorkflowRuntime workflowRuntime,
        CancellationToken cancellationToken)
    {
        var client = await workflowRuntime.CreateClientAsync(id, cancellationToken);
        return await client.DeleteAsync(cancellationToken) ? EngineResults.NoContent() : EngineResults.NotFound();
    }

    private static async Task<IResult> CancelAsync(
        [FromRoute] string id,
        [FromServices] IWorkflowCancellationService workflowCancellationService,
        CancellationToken cancellationToken)
    {
        await workflowCancellationService.CancelWorkflowAsync(id, cancellationToken);
        return EngineResults.Ok();
    }

    private static async Task<IResult> BulkCancelAsync(
        HttpContext context,
        EngineBody<BulkCancelRequest> body,
        [FromServices] IWorkflowCancellationService workflowCancellationService,
        CancellationToken cancellationToken)
    {
        var request = body.Value;
        var tasks = new List<Task<int>>();
        if (request.Ids is not null)
        {
            tasks.Add(workflowCancellationService.CancelWorkflowsAsync(request.Ids, cancellationToken));
        }

        if (request.DefinitionVersionId is not null)
        {
            tasks.Add(workflowCancellationService.CancelWorkflowByDefinitionVersionAsync(request.DefinitionVersionId, cancellationToken));
        }

        if (request.DefinitionId is not null)
        {
            tasks.Add(workflowCancellationService.CancelWorkflowByDefinitionAsync(request.DefinitionId, request.VersionOptions!.Value, cancellationToken));
        }

        await Task.WhenAll(tasks);
        return EngineResults.Json(context, new BulkCancelResponse(tasks.Sum(t => t.Result)));
    }

    /// <summary>Running instances are deleted one by one through the runtime; finished ones in bulk.</summary>
    private static async Task<IResult> BulkDeleteAsync(
        HttpContext context,
        EngineBody<BulkDeleteInstancesRequest> body,
        [FromServices] IWorkflowInstanceStore workflowInstanceStore,
        [FromServices] IWorkflowInstanceManager workflowInstanceManager,
        [FromServices] IWorkflowRuntime workflowRuntime,
        CancellationToken cancellationToken)
    {
        var request = body.Value;
        var runningFilter = new WorkflowInstanceFilter { Ids = request.Ids, DefinitionId = request.WorkflowDefinitionId, DefinitionIds = request.WorkflowDefinitionIds, WorkflowStatus = WorkflowStatus.Running };
        var count = 0L;
        foreach (var instanceId in await workflowInstanceStore.FindManyIdsAsync(runningFilter, cancellationToken))
        {
            var client = await workflowRuntime.CreateClientAsync(instanceId, cancellationToken);
            if (await client.DeleteAsync(cancellationToken))
            {
                count++;
            }
        }

        var finishedFilter = new WorkflowInstanceFilter { Ids = request.Ids, DefinitionId = request.WorkflowDefinitionId, DefinitionIds = request.WorkflowDefinitionIds, WorkflowStatus = WorkflowStatus.Finished };
        count += await workflowInstanceManager.BulkDeleteAsync(finishedFilter, cancellationToken);
        return EngineResults.Json(context, new BulkDeleteInstancesResponse(count));
    }

    internal static WorkflowInstanceModel ToModel(WorkflowInstance e) => new()
    {
        Id = e.Id,
        DefinitionId = e.DefinitionId,
        DefinitionVersionId = e.DefinitionVersionId,
        Version = e.Version,
        ParentWorkflowInstanceId = e.ParentWorkflowInstanceId,
        WorkflowState = e.WorkflowState,
        Status = e.Status,
        SubStatus = e.SubStatus,
        IsExecuting = e.IsExecuting,
        CorrelationId = e.CorrelationId,
        Name = e.Name,
        IncidentCount = e.IncidentCount,
        IsSystem = e.IsSystem,
        CreatedAt = e.CreatedAt,
        FinishedAt = e.FinishedAt,
        UpdatedAt = e.UpdatedAt
    };
}
