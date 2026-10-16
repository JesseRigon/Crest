using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Data.Protocols.Html.Models;
using Grove.Data.Protocols.Html.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Data.Protocols.Html.Workflows;

/// <summary>Sanitizes the body's HTML when the attachment's settings say so. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Data.Protocols.Html", "Content", "Sanitizes the body's HTML when the attachment's settings say so.", DisplayName = "Sanitize markup")]
[FlowNode("Done", "Refused")]
public class SanitizeMarkup : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<HtmlBodyBehaviour>().SanitizeAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Refuses a body whose Liquid does not parse, when the attachment renders Liquid. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Data.Protocols.Html", "Content", "Refuses a body whose Liquid does not parse, when the attachment renders Liquid.", DisplayName = "Check body Liquid")]
[FlowNode("Done", "Refused")]
public class CheckBodyLiquid : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var error = await context.GetRequiredService<HtmlBodyBehaviour>().LiquidErrorAsync(context.GetWorkflowInput<ContentItem>());
        if (error is null)
        {
            await context.CompleteActivityWithOutcomesAsync("Done");
            return;
        }

        Error.Set(context, error);
        Field.Set(context, $"{ContentFieldSetName.Of<HtmlBodyFieldSet>()}.{HtmlBodyFieldSet.HtmlField}");
        await context.CompleteActivityWithOutcomesAsync("Refused");
    }
}

/// <summary>
/// The HTML body's behaviour as system flows (docs/content-engine.md › Behaviour is workflows), attached to every type carrying the HtmlBody field set: the markup sanitized on creating, updating and saving a draft; its Liquid checked on validating.
/// </summary>
public sealed class HtmlBodyWorkflowProvider : IWorkflowActivityProvider, IWorkflowFlowProvider, IWorkflowFieldSetHookAttachmentProvider
{
    public const string SanitizeFlowKey = "htmlbody.sanitize";
    public const string LiquidFlowKey = "htmlbody.liquid";

    private static readonly string FieldSet = ContentFieldSetName.Of<HtmlBodyFieldSet>();

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("htmlbody.sanitize", "Sanitize markup", "content", "Grove.Data.Protocols.Html.SanitizeMarkup", "Sanitizes the body's HTML when the attachment's settings say so.", 10, Category: "Body"),
        new("htmlbody.check-liquid", "Check body Liquid", "content", "Grove.Data.Protocols.Html.CheckBodyLiquid", "Refuses a body whose Liquid does not parse, when the attachment renders Liquid.", 20, Category: "Body"),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(SanitizeFlowKey, "HTML body: sanitize", "content", ShippedFlows.Read(typeof(HtmlBodyWorkflowProvider).Assembly, "htmlbody-sanitize.json"), 10, Ownership: WorkflowOwnership.System, Version: 1),
        new(LiquidFlowKey, "HTML body: Liquid", "content", ShippedFlows.Read(typeof(HtmlBodyWorkflowProvider).Assembly, "htmlbody-liquid.json"), 20, Ownership: WorkflowOwnership.System, Version: 1),
    ];

    public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments =>
    [
        new(FieldSet, ContentLifecyclePoints.Creating, SanitizeFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Updating, SanitizeFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.SavingDraft, SanitizeFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Validating, LiquidFlowKey, 10),
    ];
}
