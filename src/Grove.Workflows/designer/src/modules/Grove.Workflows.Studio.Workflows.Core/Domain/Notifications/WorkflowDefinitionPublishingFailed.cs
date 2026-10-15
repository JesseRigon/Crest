using Grove.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Workflows.Domain.Models;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition publishing is failed.
/// </summary>
public record WorkflowDefinitionPublishingFailed(WorkflowDefinition WorkflowDefinition, ValidationErrors Errors) : INotification;