using System.Security.Claims;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Options;
using Microsoft.AspNetCore.Authorization;
using Crest.Security.Permissions;
using YesSql;

namespace Crest.Workflows.Approvals;

public sealed record ApprovalTaskModel(
    string Id, string Title, string? Description, string? Role, string? Permission, string Status,
    string? RequestedBy, DateTime CreatedUtc, string? DecidedBy, string? Comment, DateTime? DecidedUtc,
    string WorkflowInstanceId, string? WorkflowDefinitionId, string? CorrelationId, bool CanDecide);

public sealed record ApprovalDecisionRequest(string? Decision, string? Comment);

public enum ApprovalDecisionResult { Decided, NotFound, Forbidden, AlreadyDecided, Invalid }

/// <summary>
/// The approval tasks of this tenant: who may decide one (a member of its role, or a holder
/// of its permission, through Crest's authorization - member ceilings included), the
/// queue a user sees, and the decision, which is recorded first and then resumes the
/// waiting workflow. A task is decided once.
/// </summary>
public sealed class ApprovalService(ISession session, IAuthorizationService authorizationService, IPermissionService permissionService, IWorkflowResumer resumer)
{
    public Task<ApprovalTask?> FindAsync(string approvalId) =>
        session.Query<ApprovalTask, ApprovalTaskIndex>(i => i.ApprovalId == approvalId).FirstOrDefaultAsync()!;

    public async Task<IReadOnlyList<ApprovalTaskModel>> ListAsync(ClaimsPrincipal user, string? status, bool all)
    {
        var query = string.IsNullOrWhiteSpace(status)
            ? session.Query<ApprovalTask, ApprovalTaskIndex>()
            : session.Query<ApprovalTask, ApprovalTaskIndex>(i => i.Status == status);
        var tasks = await query.OrderByDescending(i => i.CreatedUtc).Take(200).ListAsync();

        var result = new List<ApprovalTaskModel>();
        foreach (var task in tasks)
        {
            var canDecide = await CanDecideAsync(user, task);
            if (all || canDecide)
            {
                result.Add(ToModel(task, canDecide && task.Status == ApprovalStatuses.Pending));
            }
        }

        return result;
    }

    public async Task<bool> CanDecideAsync(ClaimsPrincipal user, ApprovalTask task)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(task.Role) && user.IsInRole(task.Role))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(task.Permission) && await permissionService.FindByNameAsync(task.Permission) is { } permission)
        {
            return await authorizationService.AuthorizeAsync(user, permission);
        }

        return false;
    }

    public async Task<(ApprovalDecisionResult Result, ApprovalTask? Task)> DecideAsync(string approvalId, ClaimsPrincipal user, ApprovalDecisionRequest request, CancellationToken cancellationToken)
    {
        var status = request.Decision?.Trim().ToLowerInvariant() switch
        {
            "approve" or "approved" => ApprovalStatuses.Approved,
            "reject" or "rejected" => ApprovalStatuses.Rejected,
            _ => null,
        };
        if (status is null)
        {
            return (ApprovalDecisionResult.Invalid, null);
        }

        var task = await FindAsync(approvalId);
        if (task is null)
        {
            return (ApprovalDecisionResult.NotFound, null);
        }

        if (!await CanDecideAsync(user, task))
        {
            return (ApprovalDecisionResult.Forbidden, task);
        }

        if (task.Status != ApprovalStatuses.Pending)
        {
            return (ApprovalDecisionResult.AlreadyDecided, task);
        }

        task.Status = status;
        task.DecidedBy = user.Identity!.Name;
        task.DecidedByUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        task.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        task.DecidedUtc = DateTime.UtcNow;
        await session.SaveAsync(task);

        await resumer.ResumeAsync<RequestApproval>(new ApprovalStimulus(task.ApprovalId), task.WorkflowInstanceId, new ResumeBookmarkOptions(), cancellationToken);
        return (ApprovalDecisionResult.Decided, task);
    }

    public static ApprovalTaskModel ToModel(ApprovalTask t, bool canDecide) =>
        new(t.ApprovalId, t.Title, t.Description, t.Role, t.Permission, t.Status, t.RequestedBy, t.CreatedUtc, t.DecidedBy, t.Comment, t.DecidedUtc, t.WorkflowInstanceId, t.WorkflowDefinitionId, t.CorrelationId, canDecide);
}
