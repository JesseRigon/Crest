using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Management.Entities;
using JetBrains.Annotations;

namespace Grove.Workflows.Management.Notifications;

/// <summary>
/// A notification that is sent when a workflow definition is about to be retracted.
/// </summary>
/// <param name="WorkflowDefinition">The workflow definition.</param>
[PublicAPI]
public record WorkflowDefinitionRetracting(WorkflowDefinition WorkflowDefinition) : INotification;