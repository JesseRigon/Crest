using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Workflows.Domain.Models;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition version is deleting.
/// </summary>
public record WorkflowDefinitionVersionDeleting(WorkflowDefinitionVersion WorkflowDefinitionVersion) : INotification;