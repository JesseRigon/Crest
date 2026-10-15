using Grove.Workflows.Api.Client.Shared.Models;
using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Workflows.Domain.Models;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition id is exported.
/// </summary>
public record WorkflowDefinitionIdExported(string WorkflowDefinitionId, VersionOptions? VersionOptions, FileDownload FileDownload) : INotification;