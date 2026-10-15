using Grove.Workflows.Common;
using Grove.Workflows.Runtime.Stimuli;

namespace Grove.Workflows.Runtime.Bookmarks;

[Obsolete("Use EventStimulus instead.")]
[ForwardedType(typeof(EventStimulus))]
public record EventBookmarkPayload(string EventName);