using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.Registry;
using Crest.Workflows.UIHints;
using Crest.Workflows.Units;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Hooks;

/// <summary>
/// Runs every flow attached to a hook slot <em>inline, inside this flow's unit of work</em>
/// (docs/workflows.md › Posting on workflows): each attachment starts as a child instance in
/// the same scope and session, with this flow's payload and actor and the slot key as input.
/// A required attachment that faults or suspends fails the unit - this flow faults, and at
/// commit everything the burst wrote is discarded. A best-effort attachment's failure is
/// journaled and the flow goes on. Attachments are atomic by construction (checked when
/// attached and when published), so a hook never waits and never calls out.
/// </summary>
[Activity("Crest.Workflows", "Crest", "Runs the flows attached to a hook slot inside this flow's transaction.", DisplayName = "Hook")]
[FlowNode("Done")]
[UsedImplicitly]
public class Hook : Activity
{
    [Input(DisplayName = "Slot", Description = "The registered hook slot whose attachments run here, e.g. transaction.created.", UIHint = InputUIHints.DropDown, UIHandler = typeof(UIHints.HookSlotOptionsProvider))]
    public Input<string> Slot { get; set; } = null!;

    [Output(Description = "How many attachments ran.")]
    public Output<int> Ran { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var slotKey = Slot.GetOrDefault(context)?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            throw new WorkflowUnitFailedException("The Hook activity has no slot.");
        }

        // A required failure throws WorkflowHookFailedException: the engine records it as this
        // flow's incident, and the failed unit is discarded at commit.
        var result = await context.GetRequiredService<WorkflowHookRunner>().RunAsync(
            slotKey, context.WorkflowExecutionContext.CorrelationId, context.WorkflowExecutionContext.Input,
            context.WorkflowExecutionContext.Id, context.Activity.Id, context.CancellationToken);

        foreach (var failure in result.BestEffortFailures)
        {
            context.JournalData[$"BestEffort:{failure.Split(':')[0]}"] = failure;
        }

        Ran.Set(context, result.Ran);
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>
/// Fails the unit this flow runs in, with a reason: inside a hook attachment, after a Failed
/// port, it is how a flow says "the host must not go through". The flow faults with the
/// reason as its incident; the hosting burst discards every write at commit.
/// </summary>
[Activity("Crest.Workflows", "Crest", "Fails the current unit of work: the hosting flow faults and nothing written in the burst stands.", DisplayName = "Fail unit")]
[UsedImplicitly]
public class FailUnit : Activity
{
    [Input(DisplayName = "Reason", Description = "What went wrong, for the journal.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> Reason { get; set; } = null!;

    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var reason = Reason.GetOrDefault(context);
        reason = string.IsNullOrWhiteSpace(reason) ? "The flow failed its unit of work." : reason.Trim();
        context.GetRequiredService<WorkflowUnitOfWork>().Fail(reason, context.Activity.Id);
        throw new WorkflowUnitFailedException(reason);
    }
}

/// <summary>Crest.Workflows' own slot: a generic hook any flow may place and any tenant may attach to.</summary>
public sealed class WorkflowsHookSlotProvider : IWorkflowHookSlotProvider
{
    public const string FlowHook = "flow.hook";

    public IEnumerable<WorkflowHookSlotDescriptor> HookSlots =>
    [
        new(FlowHook, "Flow hook", WorkflowsConstants.Objects.Workflow, "A generic slot a flow places with the Hook activity; attachments run inside that flow's transaction.", 1000),
    ];
}
