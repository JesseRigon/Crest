using Grove.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Grove.Workflows.Studio.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition is exporting.
/// </summary>
public record WorkflowDefinitionExporting(WorkflowDefinition WorkflowDefinition) : INotification;