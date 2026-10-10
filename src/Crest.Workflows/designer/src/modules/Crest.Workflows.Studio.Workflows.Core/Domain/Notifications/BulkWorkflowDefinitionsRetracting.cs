using Crest.Workflows.Studio.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when bulk workflow definitions are retracting.
/// </summary>
public record BulkWorkflowDefinitionsRetracting(ICollection<string> WorkflowDefinitionIds) : INotification;