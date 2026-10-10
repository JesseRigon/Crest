using Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Crest.Workflows.Studio.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition is exporting.
/// </summary>
public record WorkflowDefinitionExporting(WorkflowDefinition WorkflowDefinition) : INotification;