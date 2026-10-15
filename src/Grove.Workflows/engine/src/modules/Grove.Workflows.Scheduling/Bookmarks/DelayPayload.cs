using Grove.Workflows.Scheduling.Activities;

namespace Grove.Workflows.Scheduling.Bookmarks;

/// <summary>
/// A bookmark payload for <see cref="Delay"/>.
/// </summary>
public record DelayPayload(DateTimeOffset ResumeAt);