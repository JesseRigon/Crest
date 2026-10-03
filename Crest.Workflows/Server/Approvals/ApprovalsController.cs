using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Workflows.Approvals;

/// <summary>
/// The approval queue. Any signed-in user sees the pending tasks they may decide (their
/// roles, their permissions); <c>all=true</c> lists every task and needs ManageWorkflows.
/// Deciding is 403 for someone outside the task's role and permission, 409 once decided.
/// </summary>
[ApiController, AutoValidateAntiforgeryToken, Authorize, Route(WorkflowsConstants.Routes.ApprovalsApi)]
public sealed class ApprovalsController(ApprovalService approvals, IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] string? status = ApprovalStatuses.Pending, [FromQuery] bool all = false)
    {
        if (all && !await authorizationService.AuthorizeAsync(User, Permissions.ManageWorkflows))
        {
            return Forbid();
        }

        return Ok(await approvals.ListAsync(User, status, all));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsync(string id)
    {
        var task = await approvals.FindAsync(id);
        if (task is null) return NotFound();
        var canDecide = await approvals.CanDecideAsync(User, task);
        if (!canDecide && !await authorizationService.AuthorizeAsync(User, Permissions.ManageWorkflows)) return NotFound();
        return Ok(ApprovalService.ToModel(task, canDecide && task.Status == ApprovalStatuses.Pending));
    }

    [HttpPost("{id}/decide")]
    public async Task<IActionResult> DecideAsync(string id, [FromBody] ApprovalDecisionRequest request, CancellationToken cancellationToken)
    {
        var (result, task) = await approvals.DecideAsync(id, User, request, cancellationToken);
        return result switch
        {
            ApprovalDecisionResult.Decided => Ok(ApprovalService.ToModel(task!, false)),
            ApprovalDecisionResult.Invalid => BadRequest(new { title = "Decision must be approve or reject." }),
            ApprovalDecisionResult.NotFound => NotFound(),
            ApprovalDecisionResult.Forbidden => Forbid(),
            _ => Conflict(new { title = $"Already {task!.Status}." }),
        };
    }
}
