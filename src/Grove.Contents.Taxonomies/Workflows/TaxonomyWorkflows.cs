using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Taxonomies.Models;
using Grove.Contents.Taxonomies.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Contents.Taxonomies.Workflows;

/// <summary>Writes the taxonomy's id on every term it holds, at every depth. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Taxonomies", "Content", "Writes the taxonomy's id on every term it holds, at every depth.", DisplayName = "Own terms")]
[FlowNode("Done", "Refused")]
public class OwnTerms : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<TaxonomyBehaviour>().OwnTermsAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Refuses a taxonomy that names no type for its terms. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Taxonomies", "Content", "Refuses a taxonomy that names no type for its terms.", DisplayName = "Check term type is given")]
[FlowNode("Done", "Refused")]
public class CheckTermType : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var error = await context.GetRequiredService<TaxonomyBehaviour>().TermTypeErrorAsync(context.GetWorkflowInput<ContentItem>());
        if (error is null)
        {
            await context.CompleteActivityWithOutcomesAsync("Done");
            return;
        }

        Error.Set(context, error);
        Field.Set(context, $"{ContentFieldSetName.Of<TaxonomyFieldSet>()}.{TaxonomyFieldSet.TermContentTypeField}");
        await context.CompleteActivityWithOutcomesAsync("Refused");
    }
}

/// <summary>
/// The taxonomy's behaviour as system flows (docs/content-engine.md › Behaviour is workflows), attached to every type carrying the Taxonomy field set: every term names its taxonomy on creating, updating and saving a draft; the term type is required on validating.
/// </summary>
public sealed class TaxonomyWorkflowProvider : IWorkflowActivityProvider, IWorkflowFlowProvider, IWorkflowFieldSetHookAttachmentProvider
{
    public const string OwnTermsFlowKey = "taxonomy.own-terms";
    public const string TermTypeFlowKey = "taxonomy.term-type";

    private static readonly string FieldSet = ContentFieldSetName.Of<TaxonomyFieldSet>();

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("taxonomy.own-terms", "Own terms", "content", "Grove.Contents.Taxonomies.OwnTerms", "Writes the categories' id on every term they hold, at every depth.", 10, Category: "Categories"),
        new("taxonomy.check-term-type", "Check term type is given", "content", "Grove.Contents.Taxonomies.CheckTermType", "Refuses categories that name no type for their terms.", 20, Category: "Categories"),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(OwnTermsFlowKey, "Categories: own terms", "content", ShippedFlows.Read(typeof(TaxonomyWorkflowProvider).Assembly, "taxonomy-own-terms.json"), 10, Ownership: WorkflowOwnership.System, Version: 2),
        new(TermTypeFlowKey, "Categories: term type", "content", ShippedFlows.Read(typeof(TaxonomyWorkflowProvider).Assembly, "taxonomy-term-type.json"), 20, Ownership: WorkflowOwnership.System, Version: 2),
    ];

    public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments =>
    [
        new(FieldSet, ContentLifecyclePoints.Creating, OwnTermsFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Updating, OwnTermsFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.SavingDraft, OwnTermsFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Validating, TermTypeFlowKey, 10),
    ];
}
