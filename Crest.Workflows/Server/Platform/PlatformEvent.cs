using Crest.Workflows.Attributes;
using Crest.Workflows.Expressions.Models;
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

/// <summary>The stimulus a stock event raises: its activity name (ContentPublishedEvent, UserLoggedInEvent, ...).</summary>
public sealed record PlatformEventStimulus(string EventName);

/// <summary>
/// A stock Crest workflow event as an engine trigger. Upstream modules raise events
/// through <c>IWorkflowManager.TriggerEventAsync(name, input, correlationId)</c>; the
/// tenant's manager (<see cref="PlatformWorkflowManager"/>) turns that into this stimulus.
/// On start, the stock event is instantiated with the node's properties and asked
/// <c>CanExecuteAsync</c> — so a ContentPublishedEvent's content-type filter, or any other
/// stock filter, is honoured — then resumed for its outcomes, as the stock engine would.
/// </summary>
[Activity("Crest.Workflows", "Crest", "Fires when a stock Crest workflow event is raised (content published, user logged in, ...).", DisplayName = "Crest event")]
public class PlatformEvent : Trigger<IDictionary<string, object>>
{
    [Input(DisplayName = "Event", Description = "The stock event's activity name, e.g. ContentPublishedEvent.", UIHint = InputUIHints.DropDown, UIHandler = typeof(StockEventOptionsProvider))]
    public Input<string> EventName { get; set; } = null!;

    [Input(DisplayName = "Properties (JSON)", Description = "The stock event's properties as JSON, e.g. {\"ContentTypeFilter\":[\"Item\"]}.", UIHint = InputUIHints.MultiLine)]
    public Input<string?> PropertiesJson { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        if (context.IsTriggerOfWorkflow())
            await ExecuteInternalAsync(context);
        else
            context.CreateBookmarks(GetStimuli(context.ExpressionExecutionContext), ExecuteInternalAsync, false);
    }

    protected override ValueTask<IEnumerable<object>> GetTriggerPayloadsAsync(TriggerIndexingContext context) => new(GetStimuli(context.ExpressionExecutionContext));

    private IEnumerable<object> GetStimuli(ExpressionExecutionContext context)
    {
        var name = EventName.GetOrDefault(context);
        return string.IsNullOrWhiteSpace(name) ? [] : [new PlatformEventStimulus(name.Trim())];
    }

    private async ValueTask ExecuteInternalAsync(ActivityExecutionContext context)
    {
        var runner = context.GetRequiredService<StockActivityRunner>();
        var stockEvent = runner.Instantiate(EventName.Get(context), PropertiesJson.GetOrDefault(context));
        var input = context.WorkflowExecutionContext.Input;
        var (workflowContext, activityContext) = runner.CreateContexts(
            stockEvent, context.Activity.Id, context.WorkflowExecutionContext.Id, context.WorkflowExecutionContext.Workflow.Identity.DefinitionId,
            context.WorkflowExecutionContext.CorrelationId, input, StockWorkflowStatus.Starting);

        await stockEvent.OnInputReceivedAsync(workflowContext, workflowContext.Input);

        if (!await stockEvent.CanExecuteAsync(workflowContext, activityContext))
        {
            await context.CompleteActivityWithOutcomesAsync("Skipped");
            return;
        }

        var result = await stockEvent.ResumeAsync(workflowContext, activityContext);
        context.SetResult(workflowContext.Input);
        await context.CompleteActivityWithOutcomesAsync(result.Outcomes.Any() ? result.Outcomes.ToArray() : ["Done"]);
    }
}
