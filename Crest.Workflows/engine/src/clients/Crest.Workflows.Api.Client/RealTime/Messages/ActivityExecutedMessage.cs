using Crest.Workflows.Api.Client.Resources.ActivityExecutions.Models;

namespace Crest.Workflows.Api.Client.RealTime.Messages;

/// <summary>
/// Contains information about the activity executed event.
/// </summary>
/// <param name="ActivityId">The ID of the activity.</param>
/// <param name="Stats">Execution stats about the activity.</param>
public record ActivityExecutedMessage(string ActivityId, ActivityExecutionStats Stats);