using Grove.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Grove.Workflows.Studio.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// Represents a notification sent when a workflow definition is about to be published.
public record WorkflowDefinitionPublishing(WorkflowDefinition WorkflowDefinition) : INotification;