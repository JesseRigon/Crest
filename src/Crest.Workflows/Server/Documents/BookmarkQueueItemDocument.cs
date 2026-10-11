namespace Crest.Workflows.Documents;

/// <summary>
/// A queued resume, persisted so it survives a restart and is seen by every node: the engine
/// queues a stimulus here when the bookmark it targets may not be stored yet, and the shell's
/// bookmark queue worker drains it.
/// </summary>
public class BookmarkQueueItemDocument
{
    public string ItemId { get; set; } = null!;

    public string? TenantId { get; set; }

    public string? WorkflowInstanceId { get; set; }

    public string? CorrelationId { get; set; }

    public string? BookmarkId { get; set; }

    public string? StimulusHash { get; set; }

    public string? ActivityInstanceId { get; set; }

    public string? ActivityTypeName { get; set; }

    public string? SerializedOptions { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
