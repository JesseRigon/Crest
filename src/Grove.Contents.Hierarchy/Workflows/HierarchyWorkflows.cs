using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Hierarchy.Models;
using Grove.Contents.Hierarchy.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Contents.Hierarchy.Workflows;

/// <summary>Takes the list's type and, in an ordered list, the last place for an item created into a list. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Hierarchy", "Content", "Takes the list's type and, in an ordered list, the last place for an item created into a list.", DisplayName = "File the item in its list")]
[FlowNode("Done", "Refused")]
public class FileInList : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<HierarchyBehaviour>().FileInListAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Moves an item created into a list of another culture to the list in its own culture. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Hierarchy", "Content", "Moves an item created into a list of another culture to the list in its own culture.", DisplayName = "Join the list in the item's culture")]
[FlowNode("Done", "Refused")]
public class JoinCultureList : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<HierarchyBehaviour>().JoinCultureListAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Refuses an item of a type only lists hold that names no list. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Hierarchy", "Content", "Refuses an item of a type only lists hold that names no list.", DisplayName = "Check the item has its list")]
[FlowNode("Done", "Refused")]
public class CheckListGiven : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var error = await context.GetRequiredService<HierarchyBehaviour>().ListErrorAsync(context.GetWorkflowInput<ContentItem>());
        if (error is null)
        {
            await context.CompleteActivityWithOutcomesAsync("Done");
            return;
        }

        Error.Set(context, error);
        Field.Set(context, $"{ContentFieldSetName.Of<ParentContentItemFieldSet>()}.{ParentContentItemFieldSet.ListContentItemIdField}");
        await context.CompleteActivityWithOutcomesAsync("Refused");
    }
}

/// <summary>Puts a clone of an item in an ordered list last. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Hierarchy", "Content", "Puts a clone of an item in an ordered list last.", DisplayName = "Order the clone last")]
[FlowNode("Done", "Refused")]
public class OrderCloneLast : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<HierarchyBehaviour>().OrderCloneAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Files a translation of a listed item in the translated list. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Hierarchy", "Content", "Files a translation of a listed item in the translated list.", DisplayName = "Join the translated list")]
[FlowNode("Done", "Refused")]
public class JoinTranslatedList : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<HierarchyBehaviour>().JoinTranslatedListAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Moves the children already in a translated list's culture from the list it translates. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Hierarchy", "Content", "Moves the children already in a translated list's culture from the list it translates.", DisplayName = "Gather translated children")]
[FlowNode("Done", "Refused")]
public class GatherTranslatedChildren : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<HierarchyBehaviour>().GatherTranslatedChildrenAsync(context.GetWorkflowInput<ContentItem>(), context.FindWorkflowInput<ContentItem>("PreviousContentItem"));
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>
/// The lists' behaviour as system flows (docs/content-engine.md › Behaviour is workflows): on the ParentContentItem field set welded on every type a list holds, an item is filed in its list and its culture on creating, must have its list on validating, goes last as a clone, and joins the translated list as a translation; on the ChildContentItems field set, a translated list gathers its children in its culture.
/// </summary>
public sealed class HierarchyWorkflowProvider : IWorkflowActivityProvider, IWorkflowFlowProvider, IWorkflowFieldSetHookAttachmentProvider
{
    public const string FileFlowKey = "lists.file";
    public const string CheckFlowKey = "lists.check";
    public const string CloneFlowKey = "lists.clone";
    public const string TranslateFlowKey = "lists.translate";
    public const string TranslateListFlowKey = "lists.translate-list";

    private static readonly string FieldSet = ContentFieldSetName.Of<ParentContentItemFieldSet>();

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("lists.file", "File the item in its list", "content", "Grove.Contents.Hierarchy.FileInList", "Takes the list's type and, in an ordered list, the last place for an item created into a list.", 10, Category: "Lists"),
        new("lists.join-culture", "Join the list in the item's culture", "content", "Grove.Contents.Hierarchy.JoinCultureList", "Moves an item created into a list of another culture to the list in its own culture.", 20, Category: "Lists"),
        new("lists.check-list", "Check the item has its list", "content", "Grove.Contents.Hierarchy.CheckListGiven", "Refuses an item of a type only lists hold that names no list.", 30, Category: "Lists"),
        new("lists.order-clone", "Order the clone last", "content", "Grove.Contents.Hierarchy.OrderCloneLast", "Puts a clone of an item in an ordered list last.", 40, Category: "Lists"),
        new("lists.join-translation", "Join the translated list", "content", "Grove.Contents.Hierarchy.JoinTranslatedList", "Files a translation of a listed item in the translated list.", 50, Category: "Lists"),
        new("lists.gather-translations", "Gather translated children", "content", "Grove.Contents.Hierarchy.GatherTranslatedChildren", "Moves the children already in a translated list's culture from the list it translates.", 60, Category: "Lists"),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(FileFlowKey, "Lists: file", "content", ShippedFlows.Read(typeof(HierarchyWorkflowProvider).Assembly, "lists-file.json"), 10, Ownership: WorkflowOwnership.System, Version: 1),
        new(CheckFlowKey, "Lists: check", "content", ShippedFlows.Read(typeof(HierarchyWorkflowProvider).Assembly, "lists-check.json"), 20, Ownership: WorkflowOwnership.System, Version: 1),
        new(CloneFlowKey, "Lists: clone", "content", ShippedFlows.Read(typeof(HierarchyWorkflowProvider).Assembly, "lists-clone.json"), 30, Ownership: WorkflowOwnership.System, Version: 1),
        new(TranslateFlowKey, "Lists: translate", "content", ShippedFlows.Read(typeof(HierarchyWorkflowProvider).Assembly, "lists-translate.json"), 40, Ownership: WorkflowOwnership.System, Version: 1),
        new(TranslateListFlowKey, "Lists: translate a list", "content", ShippedFlows.Read(typeof(HierarchyWorkflowProvider).Assembly, "lists-translate-list.json"), 50, Ownership: WorkflowOwnership.System, Version: 1),
    ];

    public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments =>
    [
        new(FieldSet, ContentLifecyclePoints.Creating, FileFlowKey, 20),
        new(FieldSet, ContentLifecyclePoints.Validating, CheckFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Cloning, CloneFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Localizing, TranslateFlowKey, 10),
        new(ContentFieldSetName.Of<ChildContentItemsFieldSet>(), ContentLifecyclePoints.Localizing, TranslateListFlowKey, 10),
    ];
}
