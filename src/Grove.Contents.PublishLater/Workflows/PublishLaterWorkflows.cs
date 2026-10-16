using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.PublishLater.Models;
using Grove.Contents.PublishLater.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Contents.PublishLater.Workflows;

/// <summary>Clears the item's scheduled publish once it is published. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.PublishLater", "Content", "Clears the item's scheduled publish once it is published.", DisplayName = "Clear publish schedule")]
[FlowNode("Done", "Refused")]
public class ClearPublishSchedule : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<PublishLaterBehaviour>().ClearScheduleAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>
/// Scheduled publishing's behaviour as a system flow (docs/content-engine.md › Behaviour is workflows), attached to every type carrying the PublishLater field set: the schedule is cleared when the item is published.
/// </summary>
public sealed class PublishLaterWorkflowProvider : IWorkflowActivityProvider, IWorkflowFlowProvider, IWorkflowFieldSetHookAttachmentProvider
{
    public const string ClearFlowKey = "publishlater.clear";

    private static readonly string FieldSet = ContentFieldSetName.Of<PublishLaterFieldSet>();

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("publishlater.clear-schedule", "Clear publish schedule", "content", "Grove.Contents.PublishLater.ClearPublishSchedule", "Clears the item's scheduled publish once it is published.", 10, Category: "Scheduling"),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(ClearFlowKey, "Scheduled publish: clear", "content", ShippedFlows.Read(typeof(PublishLaterWorkflowProvider).Assembly, "publishlater-clear.json"), 10, Ownership: WorkflowOwnership.System, Version: 1),
    ];

    public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments =>
    [
        new(FieldSet, ContentLifecyclePoints.Publishing, ClearFlowKey, 10),
    ];
}
