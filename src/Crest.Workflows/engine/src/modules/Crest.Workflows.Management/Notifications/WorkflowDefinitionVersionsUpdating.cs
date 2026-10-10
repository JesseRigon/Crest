using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management.Entities;
using JetBrains.Annotations;

namespace Crest.Workflows.Management.Notifications;

/// <summary>
/// A notification that is sent when specific workflow definition versions are about to be updated.
/// </summary>
[PublicAPI]
public record WorkflowDefinitionVersionsUpdating(IEnumerable<WorkflowDefinition> WorkflowDefinitions) : INotification;