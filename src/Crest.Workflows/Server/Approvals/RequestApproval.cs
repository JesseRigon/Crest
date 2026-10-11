using System.Reflection;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Contexts;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Crest.Workflows.UIHints.Dropdown;
using Microsoft.Extensions.DependencyInjection;
using Crest.Security.Services;
using YesSql;

namespace Crest.Workflows.Approvals;

/// <summary>The stimulus a decision resumes: the approval's id.</summary>
public sealed record ApprovalStimulus(string ApprovalId);

/// <summary>
/// A human decision in a flow: records an approval task for an Crest role (or the holders
/// of a permission), waits, and continues on Approved or Rejected when someone allowed to
/// decide does so (<c>api/crest/workflows/approvals</c>). Who decided and their comment are
/// outputs, for the journal and the next steps.
/// </summary>
[Activity("Crest.Workflows", "Approvals", "Waits for a member of an Crest role (or a holder of a permission) to approve or reject.", DisplayName = "Request approval")]
[FlowNode("Approved", "Rejected")]
public class RequestApproval : Activity, IUnitBoundary
{
    private const string ApprovalIdProperty = "Crest.ApprovalId";

    [Input(DisplayName = "Title", Description = "What is being approved; expressions can name the object.", UIHint = InputUIHints.SingleLine)]
    public Input<string> Title { get; set; } = null!;

    [Input(DisplayName = "Description", UIHint = InputUIHints.MultiLine)]
    public Input<string?> Description { get; set; } = null!;

    [Input(DisplayName = "Role", Description = "Members of this Crest role may decide.", UIHint = InputUIHints.DropDown, UIHandler = typeof(RoleOptionsProvider))]
    public Input<string?> Role { get; set; } = null!;

    [Input(DisplayName = "Permission", Description = "Optional. Holders of this Crest permission may decide too.", UIHint = InputUIHints.DropDown, UIHandler = typeof(PermissionOptionsProvider))]
    public Input<string?> Permission { get; set; } = null!;

    [Output(Description = "The approval task's id.")]
    public Output<string?> ApprovalId { get; set; } = null!;

    [Output(Description = "The user name of whoever decided.")]
    public Output<string?> DecidedBy { get; set; } = null!;

    [Output(Description = "The decider's comment.")]
    public Output<string?> Comment { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var role = Role.GetOrDefault(context)?.Trim();
        var permission = Permission.GetOrDefault(context)?.Trim();
        if (string.IsNullOrEmpty(role) && string.IsNullOrEmpty(permission))
        {
            throw new InvalidOperationException("Request approval needs a role or a permission: nobody could decide it.");
        }

        var workflow = context.WorkflowExecutionContext;
        var task = new ApprovalTask
        {
            ApprovalId = Guid.NewGuid().ToString("n"),
            Title = Title.GetOrDefault(context) ?? "Approval",
            Description = Description.GetOrDefault(context),
            Role = string.IsNullOrEmpty(role) ? null : role,
            Permission = string.IsNullOrEmpty(permission) ? null : permission,
            WorkflowInstanceId = workflow.Id,
            WorkflowDefinitionId = workflow.Workflow.Identity.DefinitionId,
            CorrelationId = workflow.CorrelationId,
            RequestedBy = context.FindWorkflowInput<WorkflowUserContext>(WorkflowUserContext.InputKey)?.UserName,
            CreatedUtc = DateTime.UtcNow,
        };

        var session = context.GetRequiredService<ISession>();
        await session.SaveAsync(task);

        context.SetProperty(ApprovalIdProperty, task.ApprovalId);
        ApprovalId.Set(context, task.ApprovalId);
        context.CreateBookmark(new CreateBookmarkArgs
        {
            Stimulus = new ApprovalStimulus(task.ApprovalId),
            IncludeActivityInstanceId = false,
            Callback = OnDecidedAsync,
        });
    }

    private async ValueTask OnDecidedAsync(ActivityExecutionContext context)
    {
        var approvalId = context.GetProperty<string>(ApprovalIdProperty);
        var task = approvalId is null ? null : await context.GetRequiredService<ApprovalService>().FindAsync(approvalId);
        if (task is null || task.Status == ApprovalStatuses.Pending)
        {
            throw new InvalidOperationException($"Approval '{approvalId}' was resumed without a decision.");
        }

        ApprovalId.Set(context, task.ApprovalId);
        DecidedBy.Set(context, task.DecidedBy);
        Comment.Set(context, task.Comment);
        await context.CompleteActivityWithOutcomesAsync(task.Status == ApprovalStatuses.Approved ? "Approved" : "Rejected");
    }
}

/// <summary>The tenant's Crest roles, for role inputs.</summary>
public sealed class RoleOptionsProvider(IRoleService roleService) : DropDownOptionsProviderBase
{
    protected override async ValueTask<ICollection<SelectListItem>> GetItemsAsync(PropertyInfo propertyInfo, object? context, CancellationToken cancellationToken) =>
        [new SelectListItem(string.Empty, string.Empty), .. (await roleService.GetRoleNamesAsync()).Order().Select(name => new SelectListItem(name, name))];
}
