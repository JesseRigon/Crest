using Grove.Workflows.Studio.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when bulk workflow definitions are publishing.
/// </summary>
public record BulkWorkflowDefinitionsPublishing(ICollection<string> WorkflowDefinitionIds) : INotification;