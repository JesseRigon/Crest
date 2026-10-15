using System.Text.Json.Nodes;
using Grove.Contents.Management;

namespace Grove.Contents.AuditTrail.Models;

/// <summary>
/// A content event (docs/audit.md › Content history is stored as diffs). Each event stores what
/// changed since the item's previous event (<see cref="Diff"/>); every
/// <see cref="Services.ContentHistory.CheckpointInterval"/>th event, and the first, also keeps
/// a compressed full <see cref="Checkpoint"/>; the event of the current published version
/// keeps the full <see cref="ContentItem"/>, uncompressed. Rebuilding any past version replays
/// at most a checkpoint interval of diffs. The summary fields are what a list shows without
/// rebuilding anything.
/// </summary>
public class AuditTrailContentEvent
{
    public string Name { get; set; } = "Content";

    /// <summary>
    /// The full item: set by the recorder, kept only on the current published version's event
    /// (the history moves it into <see cref="Checkpoint"/> when a newer version is published).
    /// </summary>
    public ContentItem ContentItem { get; set; }

    public int VersionNumber { get; set; }

    public string Comment { get; set; }

    /// <summary>The position of this event in the item's history, from 1.</summary>
    public int Sequence { get; set; }

    /// <summary>The <see cref="Services.JsonTreeDiff"/> operations from the previous event's version; null on the first.</summary>
    public JsonArray Diff { get; set; }

    /// <summary>The full item, gzip-compressed and base64-encoded.</summary>
    public string Checkpoint { get; set; }

    public string ContentItemId { get; set; }

    public string ContentItemVersionId { get; set; }

    public string ContentType { get; set; }

    public string DisplayText { get; set; }

    public string Owner { get; set; }

    public string Author { get; set; }

    public DateTime? ModifiedUtc { get; set; }

    public bool Published { get; set; }

    public bool Latest { get; set; }

    /// <summary>A content item carrying the summary fields only, for lists and links.</summary>
    public ContentItem Stub() => new()
    {
        ContentItemId = ContentItemId ?? ContentItem?.ContentItemId,
        ContentItemVersionId = ContentItemVersionId ?? ContentItem?.ContentItemVersionId,
        ContentType = ContentType ?? ContentItem?.ContentType,
        DisplayText = DisplayText ?? ContentItem?.DisplayText,
        Owner = Owner ?? ContentItem?.Owner,
        Author = Author ?? ContentItem?.Author,
        ModifiedUtc = ModifiedUtc ?? ContentItem?.ModifiedUtc,
        Published = Published,
        Latest = Latest,
    };
}
