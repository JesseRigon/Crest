using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Workflows.Domain.Models;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition version is reverted.
/// </summary>
public record WorkflowDefinitionVersionReverted(WorkflowDefinitionVersion WorkflowDefinitionVersion) : INotification;