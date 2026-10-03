using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management.Entities;
using Crest.Workflows.State;

namespace Crest.Workflows.Runtime.Notifications;

public record WorkflowStateCommitted(WorkflowExecutionContext WorkflowExecutionContext, WorkflowState WorkflowState, WorkflowInstance WorkflowInstance) : INotification;