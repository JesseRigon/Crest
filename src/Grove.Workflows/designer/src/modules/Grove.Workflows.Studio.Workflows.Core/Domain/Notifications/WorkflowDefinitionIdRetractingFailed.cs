using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Workflows.Domain.Models;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition id retracting is failed.
/// </summary>
public record WorkflowDefinitionIdRetractingFailed(string WorkflowDefinitionId, ValidationErrors Errors) : INotification;