using Crest.Workflows.Studio.Workflows.Domain.Models;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when bulk workflow definition are reverted.
/// </summary>
public record BulkWorkflowDefinitionReverted(ICollection<WorkflowDefinitionVersion> WorkflowDefinitionVersions);