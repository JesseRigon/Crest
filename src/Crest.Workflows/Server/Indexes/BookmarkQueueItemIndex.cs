using YesSql.Indexes;

namespace Crest.Workflows.Indexes;

public class BookmarkQueueItemIndex : MapIndex
{
    public string ItemId { get; set; } = null!;
    public string? WorkflowInstanceId { get; set; }
    public string? BookmarkId { get; set; }
    public string? StimulusHash { get; set; }
    public string? ActivityInstanceId { get; set; }
    public string? ActivityTypeName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
