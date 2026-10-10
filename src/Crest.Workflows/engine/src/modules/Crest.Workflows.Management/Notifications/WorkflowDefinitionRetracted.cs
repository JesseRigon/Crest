using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management.Entities;
using JetBrains.Annotations;

namespace Crest.Workflows.Management.Notifications;

/// <summary>
/// A notification that is sent when a workflow definition is retracted.
/// </summary>
/// <param name="WorkflowDefinition">The workflow definition.</param>
[PublicAPI]
public record WorkflowDefinitionRetracted(WorkflowDefinition WorkflowDefinition) : INotification;