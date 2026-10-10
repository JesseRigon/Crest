using Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Workflows.Domain.Models;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition is exported.
/// </summary>
public record WorkflowDefinitionExported(WorkflowDefinition WorkflowDefinition, FileDownload FileDownload) : INotification;