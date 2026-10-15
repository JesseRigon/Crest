using Grove.Workflows.Common;
using Grove.Workflows.Runtime.Activities;
using Grove.Workflows.Runtime.Stimuli;

namespace Grove.Workflows.Runtime.Bookmarks;

/// <summary>
/// Bookmark payload for the <see cref="DispatchWorkflow"/> activity.
/// </summary>
/// <param name="ChildInstanceId">The instance ID of the child workflow that was created by the <see cref="DispatchWorkflow"/> activity.</param>
[Obsolete("Use DispatchWorkflowStimulus instead.")]
[ForwardedType(typeof(DispatchWorkflowStimulus))]
public record DispatchWorkflowBookmark(string ChildInstanceId);