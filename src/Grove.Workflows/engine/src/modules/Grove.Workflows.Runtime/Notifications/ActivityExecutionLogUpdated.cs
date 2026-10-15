using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Runtime.Entities;

namespace Grove.Workflows.Runtime.Notifications;

/// <summary>
/// An event that is published when activity executions are persisted.
/// </summary>
/// <param name="WorkflowExecutionContext">The workflow execution context.</param>
/// <param name="Records">The activity execution records.</param>
public record ActivityExecutionLogUpdated(WorkflowExecutionContext WorkflowExecutionContext, ICollection<ActivityExecutionRecord> Records) : INotification;