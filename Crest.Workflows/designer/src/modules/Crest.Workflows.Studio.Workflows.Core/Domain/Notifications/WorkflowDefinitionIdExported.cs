using Crest.Workflows.Api.Client.Shared.Models;
using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Workflows.Domain.Models;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition id is exported.
/// </summary>
public record WorkflowDefinitionIdExported(string WorkflowDefinitionId, VersionOptions? VersionOptions, FileDownload FileDownload) : INotification;