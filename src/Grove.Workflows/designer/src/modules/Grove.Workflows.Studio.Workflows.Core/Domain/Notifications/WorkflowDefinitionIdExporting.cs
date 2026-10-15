using Grove.Workflows.Api.Client.Shared.Models;
using Grove.Workflows.Studio.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition id is exporting.
/// </summary>
public record WorkflowDefinitionIdExporting(string WorkflowDefinitionId, VersionOptions? VersionOptions) : INotification;