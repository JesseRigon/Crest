using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management.Entities;
using JetBrains.Annotations;

namespace Crest.Workflows.Management.Notifications;

/// <summary>
/// A notification that is sent when a workflow definition version is retracted.
/// </summary>
/// <param name="WorkflowDefinition">The workflow definition.</param>
[PublicAPI]
public record WorkflowDefinitionVersionRetracted(WorkflowDefinition WorkflowDefinition) : INotification;