using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Management.Entities;
using JetBrains.Annotations;

namespace Grove.Workflows.Management.Notifications;

/// <summary>
/// A notification that is sent when a workflow definition version is deleted.
/// </summary>
[PublicAPI]
public record WorkflowDefinitionVersionDeleted(WorkflowDefinition WorkflowDefinition) : INotification;