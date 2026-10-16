using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.ArchiveLater.Models;
using Grove.Contents.ArchiveLater.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Contents.ArchiveLater.Workflows;

/// <summary>Clears the item's scheduled archive once it is archiveed. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.ArchiveLater", "Content", "Clears the item's scheduled archive once it is archiveed.", DisplayName = "Clear archive schedule")]
[FlowNode("Done", "Refused")]
public class ClearArchiveSchedule : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<ArchiveLaterBehaviour>().ClearScheduleAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>
/// Scheduled archiveing's behaviour as a system flow (docs/content-engine.md › Behaviour is workflows), attached to every type carrying the ArchiveLater field set: the schedule is cleared when the item is archiveed.
/// </summary>
public sealed class ArchiveLaterWorkflowProvider : IWorkflowActivityProvider, IWorkflowFlowProvider, IWorkflowFieldSetHookAttachmentProvider
{
    public const string ClearFlowKey = "archivelater.clear";

    private static readonly string FieldSet = ContentFieldSetName.Of<ArchiveLaterFieldSet>();

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("archivelater.clear-schedule", "Clear archive schedule", "content", "Grove.Contents.ArchiveLater.ClearArchiveSchedule", "Clears the item's scheduled archive once it is archiveed.", 10, Category: "Scheduling"),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(ClearFlowKey, "Scheduled archive: clear", "content", ShippedFlows.Read(typeof(ArchiveLaterWorkflowProvider).Assembly, "archivelater-clear.json"), 10, Ownership: WorkflowOwnership.System, Version: 1),
    ];

    public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments =>
    [
        new(FieldSet, ContentLifecyclePoints.Unpublishing, ClearFlowKey, 10),
    ];
}
