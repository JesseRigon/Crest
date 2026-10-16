using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Localization.Models;
using Grove.Contents.Localization.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Contents.Localization.Workflows;

/// <summary>Gives a new item a localization set and the default culture when it has none. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Localization", "Content", "Gives a new item a localization set and the default culture when it has none.", DisplayName = "Set culture and localization set")]
[FlowNode("Done", "Refused")]
public class SetCultureAndLocalizationSet : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<LocalizationBehaviour>().SetCultureAndSetAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Gives a clone a localization set of its own. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Localization", "Content", "Gives a clone a localization set of its own.", DisplayName = "Clear localization set")]
[FlowNode("Done", "Refused")]
public class ClearLocalizationSet : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<LocalizationBehaviour>().ClearSetAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Updates the localization entries when a translated item is published, unpublished or removed. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Localization", "Content", "Updates the localization entries when a translated item is published, unpublished or removed.", DisplayName = "Register localization entries")]
[FlowNode("Done", "Refused")]
public class RegisterLocalizationEntries : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<LocalizationBehaviour>().RegisterEntriesAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>
/// The localization's behaviour as system flows (docs/content-engine.md › Behaviour is workflows), attached to every type carrying the Localization field set: a new item's culture and set on creating; a clone's own set on cloning; the localization entries on publishing, unpublishing and removing.
/// </summary>
public sealed class LocalizationWorkflowProvider : IWorkflowActivityProvider, IWorkflowFlowProvider, IWorkflowFieldSetHookAttachmentProvider
{
    public const string CultureFlowKey = "localization.culture";
    public const string CloneFlowKey = "localization.clone";
    public const string EntriesFlowKey = "localization.entries";

    private static readonly string FieldSet = ContentFieldSetName.Of<LocalizationFieldSet>();

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("localization.set-culture", "Set culture and localization set", "content", "Grove.Contents.Localization.SetCultureAndLocalizationSet", "Gives a new item a localization set and the default culture when it has none.", 10, Category: "Localization"),
        new("localization.clear-set", "Clear localization set", "content", "Grove.Contents.Localization.ClearLocalizationSet", "Gives a clone a localization set of its own.", 20, Category: "Localization"),
        new("localization.register-entries", "Register localization entries", "content", "Grove.Contents.Localization.RegisterLocalizationEntries", "Updates the localization entries when a translated item is published, unpublished or removed.", 30, Category: "Localization"),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(CultureFlowKey, "Localization: culture", "content", ShippedFlows.Read(typeof(LocalizationWorkflowProvider).Assembly, "localization-culture.json"), 10, Ownership: WorkflowOwnership.System, Version: 1),
        new(CloneFlowKey, "Localization: clone", "content", ShippedFlows.Read(typeof(LocalizationWorkflowProvider).Assembly, "localization-clone.json"), 20, Ownership: WorkflowOwnership.System, Version: 1),
        new(EntriesFlowKey, "Localization: entries", "content", ShippedFlows.Read(typeof(LocalizationWorkflowProvider).Assembly, "localization-entries.json"), 30, Ownership: WorkflowOwnership.System, Version: 1),
    ];

    public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments =>
    [
        new(FieldSet, ContentLifecyclePoints.Creating, CultureFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Cloning, CloneFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Publishing, EntriesFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Unpublishing, EntriesFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Removing, EntriesFlowKey, 10),
    ];
}
