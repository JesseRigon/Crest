using Crest.Workflows.Studio.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when bulk workflow definitions are retracted.
/// </summary>
public record BulkWorkflowDefinitionsRetracted(ICollection<string> WorkflowDefinitionIds) : INotification;