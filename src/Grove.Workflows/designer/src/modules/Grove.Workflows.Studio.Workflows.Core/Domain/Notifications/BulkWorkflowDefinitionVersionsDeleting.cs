using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Workflows.Domain.Models;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when bulk workflow definition versions are deleting.
/// </summary>
public record BulkWorkflowDefinitionVersionsDeleting(ICollection<WorkflowDefinitionVersion> Versions) : INotification;