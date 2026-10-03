using Crest.Workflows.Api.Client.Shared.Models;
using Crest.Workflows.Studio.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Notifications;

/// <summary>
/// Represents the notification published when a workflow definition id is exporting.
/// </summary>
public record WorkflowDefinitionIdExporting(string WorkflowDefinitionId, VersionOptions? VersionOptions) : INotification;