using Grove.Workflows.Common;
using Grove.Workflows.Attributes;
using Grove.Workflows.Runtime.Activities;
using Grove.Workflows.Runtime.Stimuli;

namespace Grove.Workflows.Runtime.Bookmarks;

/// <summary>
/// Contains information created by <see cref="RunTask"/>.  
/// </summary>
[Obsolete("Use RunTaskStimulus instead.")]
[ForwardedType(typeof(RunTaskStimulus))]
public record RunTaskBookmarkPayload(string TaskId, [property: ExcludeFromHash] string TaskName);