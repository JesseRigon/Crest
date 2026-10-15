#nullable enable

using System.Text.Json.Nodes;
using Grove.AuditTrail.Models;
using Grove.Contents.Management;
using Grove.Contents.AuditTrail.Indexes;
using Grove.Contents.AuditTrail.Models;
using Grove.Entities;
using YesSql;
using YesSql.Services;

namespace Grove.Contents.AuditTrail.Services;

/// <summary>
/// A content item's history as diffs with checkpoints (docs/audit.md › Content history is stored
/// as diffs, ruling 2026-10-06): replaces the stock snapshot-per-event. <see cref="PrepareAsync"/>
/// turns the full item a recorder put on an event into its stored form when the event is saved;
/// <see cref="RebuildAsync(AuditTrailEvent)"/> gives any recorded version back.
/// </summary>
/// <remarks>
/// Retention trims the oldest events first, so a version whose checkpoint was trimmed cannot be
/// rebuilt; <see cref="RebuildAsync(AuditTrailEvent)"/> answers null for it.
/// </remarks>
public sealed class ContentHistory(ISession session)
{
    /// <summary>Every this many events an event also keeps a compressed full version.</summary>
    public const int CheckpointInterval = 10;

    /// <summary>
    /// Stores <paramref name="contentEvent"/> as a diff from the item's previous recorded event:
    /// a checkpoint on the first event and every <see cref="CheckpointInterval"/>th, the full
    /// item on a published version (whose predecessor snapshot is compressed into a checkpoint).
    /// </summary>
    public async Task PrepareAsync(AuditTrailEvent auditTrailEvent, AuditTrailContentEvent contentEvent, bool isPublished)
    {
        var item = contentEvent.ContentItem;
        if (item is null)
        {
            return;
        }

        contentEvent.ContentItemId = item.ContentItemId;
        contentEvent.ContentItemVersionId = item.ContentItemVersionId;
        contentEvent.ContentType = item.ContentType;
        contentEvent.DisplayText = item.DisplayText;
        contentEvent.Owner = item.Owner;
        contentEvent.Author = item.Author;
        contentEvent.ModifiedUtc = item.ModifiedUtc;
        contentEvent.Published = item.Published;
        contentEvent.Latest = item.Latest;

        var current = JObject.FromObject(item)!;
        var previous = await LatestAsync(item.ContentItemId);
        contentEvent.Sequence = (previous?.Sequence ?? 0) + 1;

        var previousState = previous is null ? null : await RebuildJsonAsync(previous.ContentItemId, previous.Sequence);
        contentEvent.Diff = previousState is null ? null : JsonTreeDiff.Diff(previousState, current);

        if (isPublished)
        {
            await DemotePublishedSnapshotsAsync(item.ContentItemId, auditTrailEvent.EventId);
            contentEvent.ContentItem = item;
            contentEvent.Checkpoint = null;
        }
        else
        {
            contentEvent.ContentItem = null;
            contentEvent.Checkpoint = previousState is null || contentEvent.Sequence % CheckpointInterval == 0
                ? JsonTreeDiff.Compress(current)
                : null;
        }
    }

    /// <summary>The version an event recorded, or null when it cannot be rebuilt (trimmed history).</summary>
    public async Task<ContentItem?> RebuildAsync(AuditTrailEvent auditTrailEvent)
    {
        var contentEvent = auditTrailEvent.As<AuditTrailContentEvent>();
        if (contentEvent is null)
        {
            return null;
        }

        var json = Full(contentEvent)
            ?? (contentEvent.Sequence > 0 ? await RebuildJsonAsync(contentEvent.ContentItemId, contentEvent.Sequence) : null);

        return json?.ToObject<ContentItem>();
    }

    /// <summary>The version recorded just before <paramref name="auditTrailEvent"/>, or null.</summary>
    public async Task<ContentItem?> RebuildPreviousAsync(AuditTrailEvent auditTrailEvent)
    {
        var contentEvent = auditTrailEvent.As<AuditTrailContentEvent>();
        if (contentEvent is null || contentEvent.Sequence <= 1)
        {
            return null;
        }

        return (await RebuildJsonAsync(contentEvent.ContentItemId, contentEvent.Sequence - 1))?.ToObject<ContentItem>();
    }

    private async Task<ContentHistoryIndex?> LatestAsync(string contentItemId) =>
        await session.QueryIndex<ContentHistoryIndex>(index => index.ContentItemId == contentItemId, collection: AuditTrailEvent.Collection)
            .OrderByDescending(index => index.Sequence)
            .FirstOrDefaultAsync();

    private async Task<JsonNode?> RebuildJsonAsync(string contentItemId, int sequence)
    {
        var start = await session.QueryIndex<ContentHistoryIndex>(index =>
                index.ContentItemId == contentItemId && index.HasFullState && index.Sequence <= sequence,
                collection: AuditTrailEvent.Collection)
            .OrderByDescending(index => index.Sequence)
            .FirstOrDefaultAsync();
        if (start is null)
        {
            return null;
        }

        var events = (await session.Query<AuditTrailEvent, ContentHistoryIndex>(index =>
                index.ContentItemId == contentItemId && index.Sequence >= start.Sequence && index.Sequence <= sequence,
                collection: AuditTrailEvent.Collection)
            .OrderBy(index => index.Sequence)
            .ListAsync())
            .Select(e => e.As<AuditTrailContentEvent>())
            .ToArray();

        // A gap (an event trimmed or never saved) breaks the chain: nothing is better than a wrong version.
        if (events.Length != sequence - start.Sequence + 1 || Full(events[0]) is not { } state)
        {
            return null;
        }

        foreach (var contentEvent in events.Skip(1))
        {
            state = JsonTreeDiff.Apply(state, contentEvent.Diff);
        }

        return state;
    }

    private async Task DemotePublishedSnapshotsAsync(string contentItemId, string currentEventId)
    {
        var snapshots = await session.Query<AuditTrailEvent, ContentHistoryIndex>(index =>
                index.ContentItemId == contentItemId && index.IsPublishedSnapshot && index.EventId != currentEventId,
                collection: AuditTrailEvent.Collection)
            .ListAsync();

        foreach (var snapshot in snapshots)
        {
            // The stored form changes, the history does not: the same version stays rebuildable
            // from the checkpoint.
            snapshot.Alter<AuditTrailContentEvent>(contentEvent =>
            {
                if (contentEvent.ContentItem is not null)
                {
                    contentEvent.Checkpoint = JsonTreeDiff.Compress(JObject.FromObject(contentEvent.ContentItem)!);
                    contentEvent.ContentItem = null;
                }
            });

            await session.SaveAsync(snapshot, collection: AuditTrailEvent.Collection);
        }
    }

    private static JsonNode? Full(AuditTrailContentEvent contentEvent)
    {
        if (contentEvent.ContentItem is not null)
        {
            return JObject.FromObject(contentEvent.ContentItem);
        }

        return string.IsNullOrEmpty(contentEvent.Checkpoint) ? null : JsonTreeDiff.Decompress(contentEvent.Checkpoint);
    }
}
