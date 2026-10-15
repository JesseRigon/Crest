using Grove.Workflows.Studio.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition id is retracting.
/// </summary>
public record WorkflowDefinitionIdRetracting(string WorkflowDefinitionId) : INotification;