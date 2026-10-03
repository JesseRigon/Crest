using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Registry;

/// <summary>
/// Raises a registered trigger from inside a flow (plans/workflows.md › Posting on
/// workflows): a tenant flow's hand-off, or a system flow's "after" extension point.
/// Subscribed flows start <em>after this flow's unit commits</em>, in their own units,
/// correlated to this flow's object unless <see cref="CorrelationId"/> says otherwise, with
/// <see cref="Payload"/> - by default the payload this flow was triggered with. Raising is
/// queued, not performed: nothing leaves this unit, so a hook attachment may raise a
/// trigger (the honest "call after commit" shape), and a failed unit raises nothing. For
/// work that must happen inside this flow's transaction, use a Hook slot instead. The key must
/// be registered by a module's trigger provider (<see cref="WorkflowsTriggerProvider"/> ships
/// the generic <c>flow.raised</c>); unknown keys fail the activity instead of firing nothing.
/// </summary>
[Activity("Crest.Workflows", "Crest", "Raises a registered trigger so other flows can hook this point.", DisplayName = "Raise trigger")]
[FlowNode("Done", "Failed")]
[UsedImplicitly]
public class RaiseTrigger : Activity
{
    [Input(DisplayName = "Trigger", Description = "The registered trigger key to raise, e.g. flow.raised or a module's extension point.", UIHint = InputUIHints.DropDown, UIHandler = typeof(WorkflowTriggerOptionsProvider))]
    public Input<string> TriggerKey { get; set; } = null!;

    [Input(DisplayName = "Correlation id", Description = "Optional. The object the raised trigger is about; defaults to this workflow's correlation id.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> CorrelationId { get; set; } = null!;

    [Input(DisplayName = "Payload", Description = "Optional. The payload to raise with; defaults to the payload this workflow was triggered with.")]
    public Input<IDictionary<string, object>?> Payload { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var key = TriggerKey.GetOrDefault(context);
        if (string.IsNullOrWhiteSpace(key))
        {
            context.JournalData["Error"] = "No trigger key.";
            await context.CompleteActivityWithOutcomesAsync("Failed");
            return;
        }

        var correlationId = CorrelationId.GetOrDefault(context);
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = context.WorkflowExecutionContext.CorrelationId;
        }

        var payload = Payload.GetOrDefault(context)
            ?? context.FindWorkflowInput<IDictionary<string, object>>(WorkflowsConstants.InputKeys.Payload)
            ?? new Dictionary<string, object>();

        try
        {
            await context.GetRequiredService<IWorkflowTriggerPublisher>().PublishAsync(key, correlationId, payload, context.CancellationToken);
            context.JournalData["Trigger"] = key.Trim().ToLowerInvariant();
            await context.CompleteActivityWithOutcomesAsync("Done");
        }
        catch (InvalidOperationException ex)
        {
            context.JournalData["Error"] = ex.Message;
            await context.CompleteActivityWithOutcomesAsync("Failed");
        }
    }
}

/// <summary>
/// The triggers Crest.Workflows itself registers: <c>flow.raised</c>, the generic key a
/// flow raises (Raise trigger) for other flows to hook when no module defines a more
/// specific extension point.
/// </summary>
public sealed class WorkflowsTriggerProvider : IWorkflowTriggerProvider
{
    public const string FlowRaised = "flow.raised";

    public IEnumerable<WorkflowTriggerDescriptor> Triggers =>
    [
        new(FlowRaised, "Raised by a flow", WorkflowsConstants.Objects.Workflow, "A flow raised the generic extension trigger (Raise trigger): payload is whatever that flow passed.", 1000),
    ];
}
