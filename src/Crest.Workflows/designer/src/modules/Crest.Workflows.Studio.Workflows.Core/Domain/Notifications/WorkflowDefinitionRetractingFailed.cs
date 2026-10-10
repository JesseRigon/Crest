using Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Workflows.Domain.Models;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition retracting is failed.
/// </summary>
public record WorkflowDefinitionRetractingFailed(WorkflowDefinition WorkflowDefinition, ValidationErrors Errors) : INotification;