using Crest.Workflows.Extensions;
using Crest.Workflows;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Crest.Workflows.Contexts;

namespace Crest.Workflows.Activities;

/// <summary>
/// Gate on the acting user: continues on "Allowed" when the user captured at trigger time
/// holds the Orchard permission, "Denied" otherwise (anonymous, unknown permission, or a
/// member-class user asking for a ceilinged permission all land on Denied). Put it right
/// after a trigger to make a flow fire only for users who may cause its effects.
/// </summary>
[Activity("Crest.Workflows", "Security", "Continue only if the user who triggered the workflow holds an Orchard permission.", DisplayName = "Require permission")]
[FlowNode("Allowed", "Denied")]
[UsedImplicitly]
public class RequirePermission : Activity
{
    [Input(Description = "The Orchard permission name, e.g. ManageWorkflows or PublishContent.", UIHint = InputUIHints.DropDown, UIHandler = typeof(PermissionOptionsProvider))]
    public Input<string> Permission { get; set; } = null!;

    [Output(Description = "The user name that was evaluated, for journals.")]
    public Output<string?> UserName { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var user = context.FindWorkflowInput<WorkflowUserContext>(WorkflowUserContext.InputKey);
        var permission = Permission.Get(context);
        var authorizer = context.GetRequiredService<IWorkflowAuthorizer>();
        var allowed = await authorizer.AuthorizeAsync(user, permission, context.CancellationToken);

        UserName.Set(context, user?.UserName);
        await context.CompleteActivityWithOutcomesAsync(allowed ? "Allowed" : "Denied");
    }
}
