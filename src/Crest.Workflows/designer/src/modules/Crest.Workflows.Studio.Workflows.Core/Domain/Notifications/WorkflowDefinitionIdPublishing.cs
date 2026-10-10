using Crest.Workflows.Studio.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition id is publishing.
/// </summary>
public record WorkflowDefinitionIdPublishing(string WorkflowDefinitionId) : INotification;