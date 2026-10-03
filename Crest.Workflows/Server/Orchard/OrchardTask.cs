using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Workflows.Models;
// The engine and the stock module both define StockWorkflowExecutionContext, StockWorkflowStatus and
// StockActivity; the stock ones are meant everywhere in this file.
using StockActivity = OrchardCore.Workflows.Activities.IActivity;
using StockWorkflowExecutionContext = OrchardCore.Workflows.Models.WorkflowExecutionContext;
using StockWorkflowStatus = OrchardCore.Workflows.Models.WorkflowStatus;

namespace Crest.Workflows.Orchard;

/// <summary>
/// Runs a stock OrchardCore workflow task (CreateContentTask, AssignUserRoleTask, ...) as an
/// engine activity, inside the flow's unit, with the node's properties as the task's JSON and
/// the engine's workflow input as the stock input. The task's outcomes become the node's
/// outgoing ports (Done, Failed, ...). A task that halts ends on Halted. Tasks whose effect
/// leaves the database (<see cref="StockActivityRunner.External"/>) are refused here: they are
/// <see cref="OrchardExternalTask"/>, which runs after the unit commits.
/// </summary>
[Activity("Crest.Workflows", "Orchard", "Runs a stock OrchardCore workflow task (content, users, roles, ...) inside this flow's transaction.", DisplayName = "Orchard task")]
public class OrchardTask : Activity
{
    [Input(DisplayName = "Task", Description = "The stock task's activity name, e.g. CreateContentTask or AssignUserRoleTask.", UIHint = InputUIHints.DropDown, UIHandler = typeof(StockTaskOptionsProvider))]
    public Input<string> ActivityName { get; set; } = null!;

    [Input(DisplayName = "Properties (JSON)", Description = "The stock task's properties as JSON, e.g. {\"ContentType\":\"Item\",\"Publish\":true}.", UIHint = InputUIHints.MultiLine)]
    public Input<string?> PropertiesJson { get; set; } = null!;

    /// <summary>Whether this node may run the named task: the inline task refuses external ones, the external task refuses the rest.</summary>
    protected virtual bool Accepts(string activityName) => !StockActivityRunner.External.Contains(activityName);

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var activityName = ActivityName.Get(context);
        if (!Accepts(activityName))
        {
            throw new InvalidOperationException(StockActivityRunner.External.Contains(activityName)
                ? $"'{activityName}' has an effect outside the database; use the Orchard external task, which runs after the unit commits."
                : $"'{activityName}' stays inside the database; use the Orchard task.");
        }

        var runner = context.GetRequiredService<StockActivityRunner>();
        var task = runner.Instantiate(activityName, PropertiesJson.GetOrDefault(context));
        var (workflowContext, activityContext) = runner.CreateContexts(
            task, context.Activity.Id, context.WorkflowExecutionContext.Id, context.WorkflowExecutionContext.Workflow.Identity.DefinitionId,
            context.WorkflowExecutionContext.CorrelationId, context.WorkflowExecutionContext.Input, StockWorkflowStatus.Executing);

        await task.OnInputReceivedAsync(workflowContext, workflowContext.Input);
        var result = await task.ExecuteAsync(workflowContext, activityContext);

        if (result.IsHalted)
        {
            await context.CompleteActivityWithOutcomesAsync("Halted");
            return;
        }

        await context.CompleteActivityWithOutcomesAsync(result.Outcomes.Any() ? result.Outcomes.ToArray() : ["Done"]);
    }
}

/// <summary>
/// The stock tasks whose effect leaves the database - e-mail, SMS, notifications, HTTP - as
/// an engine background activity (plans/workflows.md › Posting on workflows): the node is
/// bookmarked, the unit commits, the task runs in a unit of its own and the flow resumes with
/// its outcomes. A unit that fails never sends.
/// </summary>
[Activity("Crest.Workflows", "Orchard", "Runs a stock OrchardCore task with an effect outside the database (e-mail, SMS, notifications, HTTP) after this flow's transaction commits.", DisplayName = "Orchard external task", Kind = ActivityKind.Task, RunAsynchronously = true)]
public class OrchardExternalTask : OrchardTask, Units.IUnitBoundary
{
    protected override bool Accepts(string activityName) => StockActivityRunner.External.Contains(activityName);
}
