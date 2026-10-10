namespace Crest.Workflows.Scheduling.Bookmarks;

public record CronBookmarkPayload(DateTimeOffset ExecuteAt, string CronExpression);