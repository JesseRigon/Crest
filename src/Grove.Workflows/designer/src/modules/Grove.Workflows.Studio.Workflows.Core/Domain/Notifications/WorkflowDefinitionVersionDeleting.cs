using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Workflows.Domain.Models;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition version is deleting.
/// </summary>
public record WorkflowDefinitionVersionDeleting(WorkflowDefinitionVersion WorkflowDefinitionVersion) : INotification;