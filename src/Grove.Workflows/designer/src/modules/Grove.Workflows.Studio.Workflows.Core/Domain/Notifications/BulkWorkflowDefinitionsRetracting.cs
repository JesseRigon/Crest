using Grove.Workflows.Studio.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when bulk workflow definitions are retracting.
/// </summary>
public record BulkWorkflowDefinitionsRetracting(ICollection<string> WorkflowDefinitionIds) : INotification;