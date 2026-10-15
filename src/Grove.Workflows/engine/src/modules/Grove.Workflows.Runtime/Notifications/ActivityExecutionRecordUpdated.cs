using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Runtime.Entities;

namespace Grove.Workflows.Runtime.Notifications;

/// <summary>
/// An event that is published when an activity execution log record is updated.
/// </summary>
/// <param name="Record">The activity execution record.</param>
public record ActivityExecutionRecordUpdated(ActivityExecutionRecord Record) : INotification;