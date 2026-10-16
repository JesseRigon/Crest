using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Display.Templates.Models;
using Grove.Display.Templates.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Display.Templates.Workflows;

/// <summary>Refuses a page body that does not parse, places an unknown component, breaks a property schema or holds an action its module refuses. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Display.Templates", "Content", "Refuses a page body that does not parse, places an unknown component, breaks a property schema or holds an action its module refuses.", DisplayName = "Check page body")]
[FlowNode("Done", "Refused")]
public class CheckPageBody : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var refusal = await context.GetRequiredService<DisplayTreeBehaviour>().RefusalAsync(context.GetWorkflowInput<ContentItem>());
        if (refusal is not { } refused)
        {
            await context.CompleteActivityWithOutcomesAsync("Done");
            return;
        }

        Error.Set(context, refused.Error);
        Field.Set(context, refused.Field);
        await context.CompleteActivityWithOutcomesAsync("Refused");
    }
}

/// <summary>
/// The page body's behaviour as a system flow (docs/content-engine.md › Behaviour is workflows), attached to every type carrying the DisplayTree field set: the body checked on validating.
/// </summary>
public sealed class DisplayTreeWorkflowProvider : IWorkflowActivityProvider, IWorkflowFlowProvider, IWorkflowFieldSetHookAttachmentProvider
{
    public const string CheckFlowKey = "displaytree.check";

    private static readonly string FieldSet = ContentFieldSetName.Of<DisplayTreeFieldSet>();

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("displaytree.check", "Check page body", "content", "Grove.Display.Templates.CheckPageBody", "Refuses a page body that does not parse, places an unknown component, breaks a property schema or holds an action its module refuses.", 10, Category: "Page body"),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(CheckFlowKey, "Page body: check", "content", ShippedFlows.Read(typeof(DisplayTreeWorkflowProvider).Assembly, "displaytree-check.json"), 10, Ownership: WorkflowOwnership.System, Version: 1),
    ];

    public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments =>
    [
        new(FieldSet, ContentLifecyclePoints.Validating, CheckFlowKey, 10),
    ];
}
