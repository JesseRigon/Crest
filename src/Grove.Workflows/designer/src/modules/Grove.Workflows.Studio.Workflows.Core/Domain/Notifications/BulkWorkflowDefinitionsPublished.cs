using Grove.Workflows.Studio.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when bulk workflow definitions are published.
/// </summary>
public record BulkWorkflowDefinitionsPublished(ICollection<string> WorkflowDefinitionIds) : INotification;