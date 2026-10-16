using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Seo.Models;
using Grove.Seo.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Seo.Workflows;

/// <summary>Refuses a canonical address with characters an address may not hold, or a Google schema that is not JSON. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Seo", "Content", "Refuses a canonical address with characters an address may not hold, or a Google schema that is not JSON.", DisplayName = "Check SEO metadata")]
[FlowNode("Done", "Refused")]
public class CheckSeoMeta : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var refusal = await context.GetRequiredService<SeoMetaBehaviour>().RefusalAsync(context.GetWorkflowInput<ContentItem>());
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
/// The SEO's behaviour as a system flow (docs/content-engine.md › Behaviour is workflows), attached to every type carrying the SeoMeta field set: the canonical address and the Google schema checked on validating.
/// </summary>
public sealed class SeoMetaWorkflowProvider : IWorkflowActivityProvider, IWorkflowFlowProvider, IWorkflowFieldSetHookAttachmentProvider
{
    public const string CheckFlowKey = "seo.check";

    private static readonly string FieldSet = ContentFieldSetName.Of<SeoMetaFieldSet>();

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("seo.check", "Check SEO metadata", "content", "Grove.Seo.CheckSeoMeta", "Refuses a canonical address with characters an address may not hold, or a Google schema that is not JSON.", 10, Category: "SEO"),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(CheckFlowKey, "SEO: check", "content", ShippedFlows.Read(typeof(SeoMetaWorkflowProvider).Assembly, "seo-check.json"), 10, Ownership: WorkflowOwnership.System, Version: 1),
    ];

    public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments =>
    [
        new(FieldSet, ContentLifecyclePoints.Validating, CheckFlowKey, 10),
    ];
}
