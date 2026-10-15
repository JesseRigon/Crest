using Grove.Api;
using Grove.Workflows.Approvals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Workflows.Api;

/// <summary>
/// <c>api/grove/workflows/approvals</c>: the approval queue. Any signed-in user sees the pending
/// tasks they may decide (their roles, their permissions); <c>all=true</c> lists every task and
/// needs ManageWorkflows. Deciding is 403 for someone outside the task's role and permission,
/// 409 once decided.
/// </summary>
public sealed class WorkflowApprovalsApi : ApiContributor
{
    public override string Name => "WorkflowApprovals";

    public override string FeatureId => WorkflowsConstants.FeatureId;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("workflows/approvals").RequireAuthorization();
        routes.MapGet("", (HttpContext http, [FromQuery] string? status = ApprovalStatuses.Pending, [FromQuery] bool all = false) =>
            ApiHandlers.For<WorkflowApprovalsHandlers>(http).ListAsync(status, all));
        routes.MapGet("{id}", (HttpContext http, [FromRoute] string id) => ApiHandlers.For<WorkflowApprovalsHandlers>(http).GetAsync(id));
        routes.MapPost("{id}/decide", (HttpContext http, [FromRoute] string id, [FromBody] ApprovalDecisionRequest request, CancellationToken cancellationToken) =>
            ApiHandlers.For<WorkflowApprovalsHandlers>(http).DecideAsync(id, request, cancellationToken));
    }
}

public sealed class WorkflowApprovalsHandlers(ApprovalService approvals, IAuthorizationService authorizationService) : ApiHandlers
{
    public async Task<IResult> ListAsync(string? status, bool all)
    {
        if (all && !await authorizationService.AuthorizeAsync(User, Permissions.ManageWorkflows))
        {
            return Forbid();
        }

        return Ok(await approvals.ListAsync(status, all));
    }

    public async Task<IResult> GetAsync(string id)
    {
        var task = await approvals.FindAsync(id);
        if (task is null) return NotFound();
        var canDecide = await approvals.CanDecideAsync(task);
        if (!canDecide && !await authorizationService.AuthorizeAsync(User, Permissions.ManageWorkflows)) return NotFound();
        return Ok(ApprovalService.ToModel(task, canDecide && task.Status == ApprovalStatuses.Pending));
    }

    public async Task<IResult> DecideAsync(string id, ApprovalDecisionRequest request, CancellationToken cancellationToken)
    {
        var (result, task) = await approvals.DecideAsync(id, request, cancellationToken);
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
