using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Alias.Models;
using Grove.Contents.Alias.Services;
using Grove.Workflows;
using Grove.Workflows.Activities.Flowchart.Attributes;
using Grove.Workflows.Attributes;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Contents.Alias.Workflows;

/// <summary>Generates an empty alias from the attachment's pattern, unique. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Alias", "Content", "Generates an empty alias from the attachment's pattern, unique.", DisplayName = "Generate alias")]
[FlowNode("Done", "Refused")]
public class GenerateAlias : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<AliasBehaviour>().GenerateAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Refuses an alias that is too long or already used by another item. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Alias", "Content", "Refuses an alias that is too long or already used by another item.", DisplayName = "Check alias is unique")]
[FlowNode("Done", "Refused")]
public class CheckAliasIsUnique : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var error = await context.GetRequiredService<AliasBehaviour>().ErrorAsync(context.GetWorkflowInput<ContentItem>());
        if (error is null)
        {
            await context.CompleteActivityWithOutcomesAsync("Done");
            return;
        }

        Error.Set(context, error);
        Field.Set(context, $"{ContentFieldSetName.Of<AliasFieldSet>()}.{AliasFieldSet.AliasField}");
        await context.CompleteActivityWithOutcomesAsync("Refused");
    }
}

/// <summary>Gives a clone an alias of its own: its source's, made unique. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Alias", "Content", "Gives a clone an alias of its own: its source's, made unique.", DisplayName = "Make alias unique")]
[FlowNode("Done", "Refused")]
public class MakeAliasUnique : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<AliasBehaviour>().MakeUniqueAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>Invalidates the cached lookup of the item's alias. The item is the one the content hook is about (its <c>ContentItem</c> input).</summary>
[Activity("Grove.Contents.Alias", "Content", "Invalidates the cached lookup of the item's alias.", DisplayName = "Invalidate alias cache")]
[FlowNode("Done", "Refused")]
public class InvalidateAliasCache : Activity
{
    [Output(Description = "Why the item was refused.")] public Output<string?> Error { get; set; } = null!;

    [Output(Description = "The field the refusal is about.")] public Output<string?> Field { get; set; } = null!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        await context.GetRequiredService<AliasBehaviour>().InvalidateAsync(context.GetWorkflowInput<ContentItem>());
        await context.CompleteActivityWithOutcomesAsync("Done");
    }
}

/// <summary>
/// The alias's behaviour as system flows (docs/content-engine.md › Behaviour is workflows), attached to every type carrying the Alias field set: generated from the pattern on creating, updating, saving a draft and importing; unique on validating; made unique for a clone; the alias cache invalidated on publishing, unpublishing and removing.
/// </summary>
public sealed class AliasWorkflowProvider : IWorkflowActivityProvider, IWorkflowFlowProvider, IWorkflowFieldSetHookAttachmentProvider
{
    public const string GenerateFlowKey = "alias.generate";
    public const string UniqueFlowKey = "alias.unique";
    public const string CloneFlowKey = "alias.clone";
    public const string CacheFlowKey = "alias.cache";

    private static readonly string FieldSet = ContentFieldSetName.Of<AliasFieldSet>();

    public IEnumerable<WorkflowActivityDescriptor> Activities =>
    [
        new("alias.generate", "Generate alias", "content", "Grove.Contents.Alias.GenerateAlias", "Generates an empty alias from the attachment's pattern, unique.", 10, Category: "Alias"),
        new("alias.check-unique", "Check alias is unique", "content", "Grove.Contents.Alias.CheckAliasIsUnique", "Refuses an alias that is too long or already used by another item.", 20, Category: "Alias"),
        new("alias.make-unique", "Make alias unique", "content", "Grove.Contents.Alias.MakeAliasUnique", "Gives a clone an alias of its own: its source's, made unique.", 30, Category: "Alias"),
        new("alias.invalidate-cache", "Invalidate alias cache", "content", "Grove.Contents.Alias.InvalidateAliasCache", "Invalidates the cached lookup of the item's alias.", 40, Category: "Alias"),
    ];

    public IEnumerable<WorkflowFlowDescriptor> Flows =>
    [
        new(GenerateFlowKey, "Alias: generate", "content", ShippedFlows.Read(typeof(AliasWorkflowProvider).Assembly, "alias-generate.json"), 10, Ownership: WorkflowOwnership.System, Version: 1),
        new(UniqueFlowKey, "Alias: unique", "content", ShippedFlows.Read(typeof(AliasWorkflowProvider).Assembly, "alias-unique.json"), 20, Ownership: WorkflowOwnership.System, Version: 1),
        new(CloneFlowKey, "Alias: clone", "content", ShippedFlows.Read(typeof(AliasWorkflowProvider).Assembly, "alias-clone.json"), 30, Ownership: WorkflowOwnership.System, Version: 1),
        new(CacheFlowKey, "Alias: cache", "content", ShippedFlows.Read(typeof(AliasWorkflowProvider).Assembly, "alias-cache.json"), 40, Ownership: WorkflowOwnership.System, Version: 1),
    ];

    public IEnumerable<WorkflowFieldSetHookAttachmentDescriptor> FieldSetHookAttachments =>
    [
        new(FieldSet, ContentLifecyclePoints.Creating, GenerateFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Updating, GenerateFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.SavingDraft, GenerateFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Importing, GenerateFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Validating, UniqueFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Cloning, CloneFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Publishing, CacheFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Unpublishing, CacheFlowKey, 10),
        new(FieldSet, ContentLifecyclePoints.Removing, CacheFlowKey, 10),
    ];
}
