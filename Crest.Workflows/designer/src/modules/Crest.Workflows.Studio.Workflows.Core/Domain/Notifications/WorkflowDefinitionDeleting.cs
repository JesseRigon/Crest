using Crest.Workflows.Studio.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition is deleting.
/// </summary>
public record WorkflowDefinitionDeleting(string WorkflowDefinitionId) : INotification;