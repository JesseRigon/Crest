using Grove.Workflows.Studio.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when bulk workflow definitions are retracted.
/// </summary>
public record BulkWorkflowDefinitionsRetracted(ICollection<string> WorkflowDefinitionIds) : INotification;