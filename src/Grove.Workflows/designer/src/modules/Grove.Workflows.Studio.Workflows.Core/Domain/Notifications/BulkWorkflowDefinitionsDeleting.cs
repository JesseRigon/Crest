using Grove.Workflows.Studio.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when bulk workflow definitions are deleting.
/// </summary>
public record BulkWorkflowDefinitionsDeleting(ICollection<string> WorkflowDefinitionIds) : INotification;