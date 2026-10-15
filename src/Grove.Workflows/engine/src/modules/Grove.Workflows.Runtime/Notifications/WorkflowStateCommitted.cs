using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Management.Entities;
using Grove.Workflows.State;

namespace Grove.Workflows.Runtime.Notifications;

public record WorkflowStateCommitted(WorkflowExecutionContext WorkflowExecutionContext, WorkflowState WorkflowState, WorkflowInstance WorkflowInstance) : INotification;