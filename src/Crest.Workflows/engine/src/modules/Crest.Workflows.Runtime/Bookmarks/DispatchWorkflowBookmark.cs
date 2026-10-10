using Crest.Workflows.Common;
using Crest.Workflows.Runtime.Activities;
using Crest.Workflows.Runtime.Stimuli;

namespace Crest.Workflows.Runtime.Bookmarks;

/// <summary>
/// Bookmark payload for the <see cref="DispatchWorkflow"/> activity.
/// </summary>
/// <param name="ChildInstanceId">The instance ID of the child workflow that was created by the <see cref="DispatchWorkflow"/> activity.</param>
[Obsolete("Use DispatchWorkflowStimulus instead.")]
[ForwardedType(typeof(DispatchWorkflowStimulus))]
public record DispatchWorkflowBookmark(string ChildInstanceId);