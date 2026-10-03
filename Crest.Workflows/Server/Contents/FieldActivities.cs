using System.Text.Json;
using Crest.Workflows.Activities.Flowchart.Attributes;
using Crest.Workflows.Attributes;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;
using Crest.Workflows.UIHints;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;

namespace Crest.Workflows.Contents;

/// <summary>
/// Shared shape of the field activities: source and target items by id (the target defaults
/// to the trigger payload's object), a mapping of rows <c>Part.Field → Part.Field</c> each
/// with its required marker, and the result on the outputs. Failures end on Failed with the
/// row named, never as an exception. Writes happen in the flow's unit: inside a hook
/// attachment to <c>transaction.created</c> the copied fields exist with the invoice or not
/// at all.
/// </summary>
public abstract class FieldActivityBase : Activity, IFieldDependencySource
{
    [Input(DisplayName = "Source item id", Description = "The content item to read from.", UIHint = InputUIHints.SingleLine)]
    public Input<string> SourceId { get; set; } = null!;

    [Input(DisplayName = "Target item id", Description = "The content item to write to. Empty = the trigger payload's ContentItemId or TransactionId.", UIHint = InputUIHints.SingleLine)]
    public Input<string?> TargetId { get; set; } = null!;

    [Input(DisplayName = "Mappings (JSON)", Description = "Rows of {\"from\":\"Part.Field\",\"to\":\"Part.Field\",\"required\":true}. A required source that is empty fails the activity; an optional one is skipped.", UIHint = InputUIHints.MultiLine)]
    public Input<string?> MappingsJson { get; set; } = null!;

    [Output(Description = "How many fields were written.")]
    public Output<int> Copied { get; set; } = null!;

    [Output(Description = "Rows skipped (optional and empty), with why.")]
    public Output<IList<string>> Skipped { get; set; } = null!;

    [Output(Description = "Why the activity ended on Failed, when it did.")]
    public Output<string?> Failure { get; set; } = null!;

    protected abstract bool Move { get; }

    /// <summary>The mapping rows are this activity's declared dependencies: each source with its own marker, each target required (a copy into a missing field fails).</summary>
    public IEnumerable<WorkflowFieldDependencyDescriptor> GetFieldDependencies()
    {
        if (MappingsJson?.Expression is not { Type: "Literal" } expression)
        {
            return [];
        }

        IReadOnlyList<FieldMapping> rows;
        try
        {
            rows = ContentFieldValueCopier.ParseMappings(expression.Value?.ToString());
        }
        catch (JsonException)
        {
            return [];
        }

        return rows.SelectMany(row => new[]
        {
            new WorkflowFieldDependencyDescriptor(row.From, row.Required, Description: $"read for {row.To}"),
            new WorkflowFieldDependencyDescriptor(row.To, true, Description: $"written from {row.From}", Writes: true),
        });
    }

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var sourceId = SourceId.GetOrDefault(context);
        var targetId = TargetId.GetOrDefault(context);
        if (string.IsNullOrWhiteSpace(targetId))
        {
            var payload = context.FindWorkflowInput<IDictionary<string, object>>(WorkflowsConstants.InputKeys.Payload);
            targetId = payload is not null && (payload.TryGetValue("ContentItemId", out var id) || payload.TryGetValue("TransactionId", out id)) ? id?.ToString() : null;
        }

        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
        {
            await FailAsync(context, "A source and a target item id are required.");
            return;
        }

        IReadOnlyList<FieldMapping> mappings;
        try
        {
            mappings = ContentFieldValueCopier.ParseMappings(MappingsJson.GetOrDefault(context));
        }
        catch (JsonException ex)
        {
            await FailAsync(context, $"Mappings must be a JSON array of rows: {ex.Message}");
            return;
        }

        if (mappings.Count == 0)
        {
            await FailAsync(context, "No mappings.");
            return;
        }

        var contentManager = context.GetRequiredService<IContentManager>();
        var source = await contentManager.GetAsync(sourceId, VersionOptions.Latest);
        var target = sourceId == targetId ? source : await contentManager.GetAsync(targetId, VersionOptions.Latest);
        if (source is null || target is null)
        {
            await FailAsync(context, $"Item '{(source is null ? sourceId : targetId)}' was not found.");
            return;
        }

        var result = await context.GetRequiredService<ContentFieldValueCopier>().CopyAsync(source, target, mappings, Move);
        Copied.Set(context, result.Copied);
        Skipped.Set(context, result.Outcomes.Where(o => o.Skipped is not null).Select(o => $"{o.From} → {o.To}: {o.Skipped}").ToList());
        if (!result.Succeeded)
        {
            await FailAsync(context, result.Failure!);
            return;
        }

        if (result.Copied > 0)
        {
            await contentManager.UpdateAsync(target);
            if (Move && !ReferenceEquals(source, target))
            {
                await contentManager.UpdateAsync(source);
            }
        }

        context.JournalData["Copied"] = result.Outcomes.Where(o => o.Copied).Select(o => $"{o.From} → {o.To}").ToList();
        await context.CompleteActivityWithOutcomesAsync("Done");
    }

    private async ValueTask FailAsync(ActivityExecutionContext context, string reason)
    {
        Failure.Set(context, reason);
        context.JournalData["Error"] = reason;
        context.GetRequiredService<ILogger<FieldActivityBase>>().LogWarning("{Activity} failed: {Reason}", GetType().Name, reason);
        await context.CompleteActivityWithOutcomesAsync("Failed");
    }
}

/// <summary>Copies field values from one content item to another by a mapping; the source keeps its values.</summary>
[Activity("Crest.Workflows", "Content", "Copies field values from one content item into another by a mapping of Part.Field rows, each required or optional.", DisplayName = "Copy fields")]
[FlowNode("Done", "Failed")]
[UsedImplicitly]
public class CopyFields : FieldActivityBase
{
    protected override bool Move => false;
}

/// <summary>Copies field values and clears them on the source.</summary>
[Activity("Crest.Workflows", "Content", "Moves field values from one content item into another: copied by the mapping, then cleared on the source.", DisplayName = "Move fields")]
[FlowNode("Done", "Failed")]
[UsedImplicitly]
public class MoveFields : FieldActivityBase
{
    protected override bool Move => true;
}
