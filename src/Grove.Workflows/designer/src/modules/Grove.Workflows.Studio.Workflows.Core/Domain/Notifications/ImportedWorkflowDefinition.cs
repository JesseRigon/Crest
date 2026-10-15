using Grove.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Grove.Workflows.Studio.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when an imported workflow is definition.
/// </summary>
public record ImportedWorkflowDefinition(WorkflowDefinition WorkflowDefinition) : INotification;