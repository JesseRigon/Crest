using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management.Entities;
using JetBrains.Annotations;

namespace Crest.Workflows.Management.Notifications;

/// <summary>
/// A notification that is sent when a workflow definition is about to be retracted.
/// </summary>
/// <param name="WorkflowDefinition">The workflow definition.</param>
[PublicAPI]
public record WorkflowDefinitionRetracting(WorkflowDefinition WorkflowDefinition) : INotification;