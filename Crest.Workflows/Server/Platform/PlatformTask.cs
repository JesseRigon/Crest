using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using Microsoft.Extensions.DependencyInjection;
using Crest.Workflows.Platform.Models;
// The engine and the stock module both define StockWorkflowExecutionContext, StockWorkflowStatus and
// StockActivity; the stock ones are meant everywhere in this file.
using StockActivity = Crest.Workflows.Platform.Activities.IActivity;
using StockWorkflowExecutionContext = Crest.Workflows.Platform.Models.WorkflowExecutionContext;
using StockWorkflowStatus = Crest.Workflows.Platform.Models.WorkflowStatus;

namespace Crest.Workflows.Platform;

/// <summary>
/// Runs a stock Crest workflow task (CreateContentTask, AssignUserRoleTask, ...) as an
/// engine activity, inside the flow's unit, with the node's properties as the task's JSON and
/// the engine's workflow input as the stock input. The task's outcomes become the node's
/// outgoing ports (Done, Failed, ...). A task that halts ends on Halted. Tasks whose effect
/// leaves the database (<see cref="StockActivityRunner.External"/>) are refused here: they are
/// <see cref="PlatformExternalTask"/>, which runs after the unit commits.
/// </summary>
[Activity("Crest.Workflows", "Crest", "Runs a stock Crest workflow task (content, users, roles, ...) inside this flow's transaction.", DisplayName = "Crest task")]
public class PlatformTask : Activity
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
                ? $"'{activityName}' has an effect outside the database; use the Crest external task, which runs after the unit commits."
                : $"'{activityName}' stays inside the database; use the Crest task.");
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
/// an engine background activity (docs/workflows.md › Posting on workflows): the node is
/// bookmarked, the unit commits, the task runs in a unit of its own and the flow resumes with
/// its outcomes. A unit that fails never sends.
/// </summary>
[Activity("Crest.Workflows", "Crest", "Runs a stock Crest task with an effect outside the database (e-mail, SMS, notifications, HTTP) after this flow's transaction commits.", DisplayName = "Crest external task", Kind = ActivityKind.Task, RunAsynchronously = true)]
public class PlatformExternalTask : PlatformTask, Units.IUnitBoundary
{
    protected override bool Accepts(string activityName) => StockActivityRunner.External.Contains(activityName);
}
