using Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Crest.Workflows.Studio.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when an importing workflow is definition.
/// </summary>
public record ImportingWorkflowDefinition(WorkflowDefinitionModel WorkflowDefinition) : INotification;