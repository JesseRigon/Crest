using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Management.Entities;
using JetBrains.Annotations;

namespace Grove.Workflows.Management.Notifications;

/// <summary>
/// A notification that is sent when a workflow definition is retracted.
/// </summary>
/// <param name="WorkflowDefinition">The workflow definition.</param>
[PublicAPI]
public record WorkflowDefinitionRetracted(WorkflowDefinition WorkflowDefinition) : INotification;