using Crest.Workflows.Scheduling.Activities;

namespace Crest.Workflows.Scheduling.Bookmarks;

/// <summary>
/// A bookmark payload for <see cref="Delay"/>.
/// </summary>
public record DelayPayload(DateTimeOffset ResumeAt);