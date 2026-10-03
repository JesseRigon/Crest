using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Entities;

namespace Crest.Workflows.Runtime.Notifications;

/// <summary>
/// An event that is published when an activity execution log record is deleted.
/// </summary>
/// <param name="Record">The activity execution record.</param>
public record ActivityExecutionRecordDeleted(ActivityExecutionRecord Record) : INotification;