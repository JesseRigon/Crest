using Grove.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Grove.Workflows.Studio.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// Represents a notification sent when a workflow definition has been saved.
public record WorkflowDefinitionSaved(WorkflowDefinition WorkflowDefinition) : INotification;