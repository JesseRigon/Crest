using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Entities;

namespace Crest.Workflows.Runtime.Notifications;

/// <summary>
/// An event that is published when an activity execution log record is updated.
/// </summary>
/// <param name="Record">The activity execution record.</param>
public record ActivityExecutionRecordUpdated(ActivityExecutionRecord Record) : INotification;