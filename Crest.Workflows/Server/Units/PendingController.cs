using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YesSql;
using YesSql.Services;

namespace Crest.Workflows.Units;

/// <summary>
/// The pending state an object's page shows (docs/workflows.md › Posting on workflows: the
/// read-your-writes latency the event side costs): the flows about the object that are still
/// running or waiting, and the background jobs - external calls - scheduled for them.
/// Reading takes View.
/// </summary>
[ApiController, AutoValidateAntiforgeryToken, Route(WorkflowsConstants.Routes.PendingApi)]
public sealed class PendingController(IWorkflowInstanceStore instances, ISession session, IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] string correlationId, CancellationToken cancellationToken)
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
