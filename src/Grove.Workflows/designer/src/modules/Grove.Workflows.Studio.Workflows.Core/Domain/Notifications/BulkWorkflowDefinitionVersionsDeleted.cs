using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Workflows.Domain.Models;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when bulk workflow definition versions are deleted.
/// </summary>
public record BulkWorkflowDefinitionVersionsDeleted(ICollection<WorkflowDefinitionVersion> Versions) : INotification;