using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Crest.Workflows.Contexts;

namespace OrchardCore.Crest.Workflows.Activities;

/// <summary>
/// Gate on the acting user: continues on "Allowed" when the user captured at trigger time
/// holds the Orchard permission, "Denied" otherwise (anonymous, unknown permission, or a
/// member-class user asking for a ceilinged permission all land on Denied). Put it right
/// after a trigger to make a flow fire only for users who may cause its effects.
/// </summary>
[Activity("Crest", "Security", "Continue only if the user who triggered the workflow holds an Orchard permission.", DisplayName = "Require permission")]
[FlowNode("Allowed", "Denied")]
[UsedImplicitly]
public class RequirePermission : Activity
{
    [Input(Description = "The Orchard permission name, e.g. ManageWorkflows or PublishContent.", UIHint = InputUIHints.SingleLine)]
    public Input<string> Permission { get; set; } = null!;

    [Output(Description = "The user name that was evaluated, for journals.")]
    public Output<string?> UserName { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var user = context.GetWorkflowInput<WorkflowUserContext>(WorkflowUserContext.InputKey);
        var permission = Permission.Get(context);
        var authorizer = context.GetRequiredService<IWorkflowAuthorizer>();
        var allowed = await authorizer.AuthorizeAsync(user, permission, context.CancellationToken);

        UserName.Set(context, user?.UserName);
        await context.CompleteActivityWithOutcomesAsync(allowed ? "Allowed" : "Denied");
    }
}
