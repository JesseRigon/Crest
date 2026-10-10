using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management.Entities;
using JetBrains.Annotations;

namespace Crest.Workflows.Management.Notifications;

/// <summary>
/// A notification that is sent when specific workflow definition versions are updated.
/// </summary>
[PublicAPI]
public record WorkflowDefinitionVersionsUpdated(IEnumerable<WorkflowDefinition> WorkflowDefinitions) : INotification;