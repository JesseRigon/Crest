using Crest.Workflows.Mediator.Contracts;

namespace Crest.Workflows.Runtime.Notifications;

public record WorkflowCancelling(string WorkflowInstanceId) : INotification;