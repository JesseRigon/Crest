using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Title.Models;
using Grove.Contents.Title.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Contents.Title.Workflows;

/// <summary>
/// Sets the item a content hook is about (its <c>ContentItem</c> input) to show its title: the
/// display text follows the title, or the title is computed from the attachment's pattern.
/// </summary>
[Activity("Grove.Contents.Title", "Content", "Sets the item's display text from its title, or computes the title from the attachment's pattern.", DisplayName = "Set display text from title")]
[FlowNode("Done", "Refused")]
public class SetDisplayTextFromTitle : Activity
{
    [Output(Description = "Why the item was refused (never: the display text is always set).")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<TitleBehaviour>().SetDisplayTextAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Refuses an item whose title attachment requires a title it lacks, under <c>Title.Title</c>.</summary>
[Activity("Grove.Contents.Title", "Content", "Refuses an item without a title when its title attachment requires one.", DisplayName = "Check title is given")]
[FlowNode("Done", "Refused")]
public class CheckTitleGiven : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var error = await context.GetRequiredService<TitleBehaviour>().RequiredErrorAsync(context.GetWorkflowInput<ContentItem>());
        if (error is null)
        {
            await context.CompleteActivityWithOutcomesAsync("Done");
            return;
        }

        Error.Set(context, error);
        Field.Set(context, $"{ContentFieldSetName.Of<TitleFieldSet>()}.{TitleFieldSet.TitleField}");
        await context.CompleteActivityWithOutcomesAsync("Refused");
    }
}

/// <summary>
/// The title's behaviour as system flows (docs/content-engine.md › Behaviour is workflows),
/// attached to every type carrying the Title field set: the display text on creating, updating,
/// saving a draft, cloning and importing; the required check on validating.
/// </summary>
public sealed class TitleWorkflowProvider : IWorkflowActivityProvider, IWorkflowFlowProvider, IWorkflowFieldSetHookAttachmentProvider
{
    public const string DisplayTextFlowKey = "title.display-text";
    public const string RequiredFlowKey = "title.required";

    private static readonly string FieldSet = ContentFieldSetName.Of<TitleFieldSet>();

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("title.set-display-text", "Set display text from title", "content", "Grove.Contents.Title.SetDisplayTextFromTitle", "Sets the item's display text from its title, or computes the title from the attachment's pattern.", 10, Category: "Title"),
        new("title.check-given", "Check title is given", "content", "Grove.Contents.Title.CheckTitleGiven", "Refuses an item without a title when its title attachment requires one.", 20, Category: "Title"),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(DisplayTextFlowKey, "Title: display text", "content", ShippedFlows.Read(typeof(TitleWorkflowProvider).Assembly, "title-display-text.json"), 10, Ownership: WorkflowOwnership.System, Version: 1),
        new(RequiredFlowKey, "Title: required", "content", ShippedFlows.Read(typeof(TitleWorkflowProvider).Assembly, "title-required.json"), 20, Ownership: WorkflowOwnership.System, Version: 1),
    ];

    public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments =>
    [
        new(FieldSet, ContentLifecyclePoints.Creating, DisplayTextFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Updating, DisplayTextFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.SavingDraft, DisplayTextFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Cloning, DisplayTextFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Importing, DisplayTextFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Validating, RequiredFlowKey, 10),
    ];
}
