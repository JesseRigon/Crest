using Grove.Api;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Filters;
using Grove.Workflows.Units;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using YesSql;
using YesSql.Services;

namespace Grove.Workflows.Api;

/// <summary>
/// <c>api/grove/workflows/pending</c>: the pending state an object's page shows (docs/workflows.md
/// › Posting on workflows: the read-your-writes latency the event side costs): the flows about the
/// object that are still running or waiting, and the background jobs - external calls - scheduled
/// for them. Reading takes View.
/// </summary>
public sealed class WorkflowPendingApi : ApiContributor
{
    public override string Name => "WorkflowPending";

    public override string FeatureId => WorkflowsConstants.FeatureId;

    public override void Map(IEndpointRouteBuilder group) =>
        group.MapGet("workflows/pending", (HttpContext http, [FromQuery] string? correlationId, CancellationToken cancellationToken) =>
            ApiHandlers.For<WorkflowPendingHandlers>(http).GetAsync(correlationId, cancellationToken));
}

public sealed class WorkflowPendingHandlers(IWorkflowInstanceStore instances, YesSql.ISession session, IAuthorizationService authorizationService) : ApiHandlers
{
    public async Task<IResult> GetAsync(string? correlationId, CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.ViewWorkflows))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return BadRequest("A correlationId is required.");
        }

        var running = (await instances.FindManyAsync(new WorkflowInstanceFilter { CorrelationId = correlationId, WorkflowStatus = WorkflowStatus.Running }, cancellationToken)).ToList();
        var ids = running.Select(i => i.Id).ToList();
        var jobs = ids.Count == 0
            ? []
            : (await session.Query<WorkflowBackgroundJob, WorkflowBackgroundJobIndex>(j => j.WorkflowInstanceId.IsIn(ids) && (j.Status == BackgroundJobStatuses.Scheduled || j.Status == BackgroundJobStatuses.Running || j.Status == BackgroundJobStatuses.Created)).ListAsync(cancellationToken)).ToList();

        return Ok(new WorkflowPendingModel(
            correlationId,
            running.Select(i => new WorkflowPendingInstanceModel(i.Id, i.DefinitionId, i.Name, i.Status.ToString(), i.SubStatus.ToString(), i.CreatedAt, i.UpdatedAt)).ToList(),
            jobs.Select(j => new WorkflowPendingJobModel(j.JobId, j.WorkflowInstanceId, j.ActivityNodeId, j.Status, j.Attempts, j.CreatedUtc, j.StartedUtc, j.Error)).ToList()));
    }
}
