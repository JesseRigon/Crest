using Crest.Workflows.Common.Entities;
using Crest.Workflows.Models;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.OrderDefinitions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Crest.Workflows.Api.Endpoints.ActivityExecutions;

/// <summary>The activity execution record reads: list, summaries, count, one record, the stats report.</summary>
public static class ActivityExecutionEndpoints
{
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/activity-executions/list", ListAsync).RequireAuthorization(EngineApiPolicies.ReadActivityExecution);
        routes.MapGet("/activity-executions/count", CountAsync).RequireAuthorization(EngineApiPolicies.ReadActivityExecution);
        routes.MapGet("/activity-executions/{id}", GetAsync).RequireAuthorization(EngineApiPolicies.ReadActivityExecution);
        routes.MapPost("/activity-executions/report", ReportAsync).RequireAuthorization(EngineApiPolicies.ReadActivityExecution);
        routes.MapGet("/activity-execution-summaries/list", ListSummariesAsync).RequireAuthorization(EngineApiPolicies.ReadActivityExecution);
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        [FromQuery] string workflowInstanceId,
        [FromQuery] string activityNodeId,
        [FromQuery] bool? completed,
        [FromServices] IActivityExecutionStore store,
        CancellationToken cancellationToken)
    {
        var filter = new ActivityExecutionRecordFilter { WorkflowInstanceId = workflowInstanceId, ActivityNodeId = activityNodeId, Completed = completed };
        var order = new ActivityExecutionRecordOrder<DateTimeOffset>(x => x.StartedAt, OrderDirection.Ascending);
        var records = (await store.FindManyAsync(filter, order, cancellationToken)).ToList();
        return EngineResults.Json(context, new ListResponse<ActivityExecutionRecord>(records));
    }

    private static async Task<IResult> ListSummariesAsync(
        HttpContext context,
        [FromQuery] string workflowInstanceId,
        [FromQuery] string activityNodeId,
        [FromQuery] bool? completed,
        [FromServices] IActivityExecutionStore store,
        CancellationToken cancellationToken)
    {
        var filter = new ActivityExecutionRecordFilter { WorkflowInstanceId = workflowInstanceId, ActivityNodeId = activityNodeId, Completed = completed };
        var order = new ActivityExecutionRecordOrder<DateTimeOffset>(x => x.StartedAt, OrderDirection.Ascending);
        var records = (await store.FindManySummariesAsync(filter, order, cancellationToken)).ToList();
        return EngineResults.Json(context, new ListResponse<ActivityExecutionRecordSummary>(records));
    }

    private static async Task<IResult> CountAsync(
        HttpContext context,
        [FromQuery] string workflowInstanceId,
        [FromQuery] string activityId,
        [FromQuery] bool? completed,
        [FromServices] IActivityExecutionStore store,
        CancellationToken cancellationToken)
    {
        var filter = new ActivityExecutionRecordFilter { WorkflowInstanceId = workflowInstanceId, ActivityId = activityId, Completed = completed };
        var count = await store.CountAsync(filter, cancellationToken);
        return EngineResults.Json(context, new CountResponse(count));
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        [FromRoute] string id,
        [FromServices] IActivityExecutionStore store,
        CancellationToken cancellationToken)
    {
        var record = await store.FindAsync(new ActivityExecutionRecordFilter { Id = id }, cancellationToken);
        return record == null ? EngineResults.NotFound() : EngineResults.Json(context, record);
    }

    /// <summary>Execution counts per activity, split by completed and not, plus the blocking ones.</summary>
    private static async Task<IResult> ReportAsync(
        HttpContext context,
        EngineBody<ActivityExecutionReportRequest> body,
        [FromServices] IActivityExecutionStatsService activityExecutionStatsService,
        CancellationToken cancellationToken)
    {
        var request = body.Value;
        var stats = (await activityExecutionStatsService.GetStatsAsync(request.WorkflowInstanceId, request.ActivityNodeIds ?? [], cancellationToken)).ToList();
        return EngineResults.Json(context, new ActivityExecutionReportResponse { Stats = stats });
    }
}

public class ActivityExecutionReportRequest
{
    public string WorkflowInstanceId { get; set; } = null!;

    public ICollection<string>? ActivityNodeIds { get; set; }
}

public class ActivityExecutionReportResponse
{
    public ICollection<ActivityExecutionStats> Stats { get; set; } = new List<ActivityExecutionStats>();
}
