namespace Grove.Workflows.Scheduling.Bookmarks;

public record TimerTriggerPayload(DateTimeOffset StartAt, TimeSpan Interval);