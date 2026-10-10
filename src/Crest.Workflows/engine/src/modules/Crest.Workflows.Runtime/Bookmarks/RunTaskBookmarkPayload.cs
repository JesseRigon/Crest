using Crest.Workflows.Common;
using Crest.Workflows.Attributes;
using Crest.Workflows.Runtime.Activities;
using Crest.Workflows.Runtime.Stimuli;

namespace Crest.Workflows.Runtime.Bookmarks;

/// <summary>
/// Contains information created by <see cref="RunTask"/>.  
/// </summary>
[Obsolete("Use RunTaskStimulus instead.")]
[ForwardedType(typeof(RunTaskStimulus))]
public record RunTaskBookmarkPayload(string TaskId, [property: ExcludeFromHash] string TaskName);