using Crest.Workflows.Common;
using Crest.Workflows.Runtime.Stimuli;

namespace Crest.Workflows.Runtime.Bookmarks;

[Obsolete("Use EventStimulus instead.")]
[ForwardedType(typeof(EventStimulus))]
public record EventBookmarkPayload(string EventName);