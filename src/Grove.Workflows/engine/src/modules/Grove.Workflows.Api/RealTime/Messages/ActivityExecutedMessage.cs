using Grove.Workflows.Runtime;

namespace Grove.Workflows.Api.RealTime.Messages;

/// <summary>
/// Contains information about the activity executed event.
/// </summary>
/// <param name="ActivityId">The ID of the activity.</param>
/// <param name="Stats">Execution stats about the activity.</param>
public record ActivityExecutedMessage(string ActivityId, ActivityExecutionStats Stats);