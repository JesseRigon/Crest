using Grove.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Workflows.Domain.Models;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition retracting is failed.
/// </summary>
public record WorkflowDefinitionRetractingFailed(WorkflowDefinition WorkflowDefinition, ValidationErrors Errors) : INotification;