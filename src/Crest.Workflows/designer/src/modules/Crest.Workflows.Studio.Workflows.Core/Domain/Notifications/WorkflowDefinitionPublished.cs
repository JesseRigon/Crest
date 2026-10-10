using Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Crest.Workflows.Studio.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// Represents a notification sent when a workflow definition is published.
public record WorkflowDefinitionPublished(WorkflowDefinition WorkflowDefinition) : INotification;