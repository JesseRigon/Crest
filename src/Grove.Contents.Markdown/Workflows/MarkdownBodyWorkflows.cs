using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Markdown.Models;
using Grove.Contents.Markdown.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Contents.Markdown.Workflows;

/// <summary>Refuses a Markdown body whose Liquid does not parse, when the attachment renders Liquid. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Markdown", "Content", "Refuses a Markdown body whose Liquid does not parse, when the attachment renders Liquid.", DisplayName = "Check Markdown Liquid")]
[FlowNode("Done", "Refused")]
public class CheckMarkdownLiquid : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var error = await context.GetRequiredService<MarkdownBodyBehaviour>().LiquidErrorAsync(context.GetWorkflowInput<ContentItem>());
        if (error is null)
        {
            await context.CompleteActivityWithOutcomesAsync("Done");
            return;
        }

        Error.Set(context, error);
        Field.Set(context, $"{ContentFieldSetName.Of<MarkdownBodyFieldSet>()}.{MarkdownBodyFieldSet.MarkdownField}");
        await context.CompleteActivityWithOutcomesAsync("Refused");
    }
}

/// <summary>
/// The Markdown body's behaviour as a system flow (docs/content-engine.md › Behaviour is workflows), attached to every type carrying the MarkdownBody field set: its Liquid checked on validating.
/// </summary>
public sealed class MarkdownBodyWorkflowProvider : IWorkflowActivityProvider, IWorkflowFlowProvider, IWorkflowFieldSetHookAttachmentProvider
{
    public const string LiquidFlowKey = "markdownbody.liquid";

    private static readonly string FieldSet = ContentFieldSetName.Of<MarkdownBodyFieldSet>();

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("markdownbody.check-liquid", "Check Markdown Liquid", "content", "Grove.Contents.Markdown.CheckMarkdownLiquid", "Refuses a Markdown body whose Liquid does not parse, when the attachment renders Liquid.", 10, Category: "Body"),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(LiquidFlowKey, "Markdown body: Liquid", "content", ShippedFlows.Read(typeof(MarkdownBodyWorkflowProvider).Assembly, "markdownbody-liquid.json"), 10, Ownership: WorkflowOwnership.System, Version: 1),
    ];

    public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments =>
    [
        new(FieldSet, ContentLifecyclePoints.Validating, LiquidFlowKey, 10),
    ];
}
