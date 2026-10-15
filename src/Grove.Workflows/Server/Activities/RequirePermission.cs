using Grove.Access;
using Grove.Workflows.Extensions;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Models;
using Grove.Workflows.UIHints;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Grove.Workflows.Contexts;

namespace Grove.Workflows.Activities;

/// <summary>
/// Gate on the acting user: continues on "Allowed" when the user captured at trigger time
/// holds the Grove permission, "Denied" otherwise (anonymous, unknown permission, or a
/// member-class user asking for a ceilinged permission all land on Denied). Put it right
/// after a trigger to make a flow fire only for users who may cause its effects.
/// </summary>
[Activity("Grove.Workflows", "Security", "Continue only if the user who triggered the workflow holds an Grove permission.", DisplayName = "Require permission")]
[FlowNode("Allowed", "Denied")]
[UsedImplicitly]
public class RequirePermission : Activity
{
    [Input(Description = "The Grove permission name, e.g. ManageWorkflows or PublishContent.", UIHint = InputUIHints.DropDown, UIHandler = typeof(PermissionOptionsProvider))]
    public Input<string> Permission { get; set; } = null!;

    [Output(Description = "The user name that was evaluated, for journals.")]
    public Output<string?> UserName { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var permission = Permission.Get(context);
        var authorizer = context.GetRequiredService<IWorkflowAuthorizer>();
        var allowed = await authorizer.AuthorizeAsync(permission, context.CancellationToken);

        UserName.Set(context, context.GetRequiredService<ICallerContextAccessor>().Current?.UserName);
        await context.CompleteActivityWithOutcomesAsync(allowed ? "Allowed" : "Denied");
    }
}
