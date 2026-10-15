using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Management.Entities;
using JetBrains.Annotations;

namespace Grove.Workflows.Management.Notifications;

/// <summary>
/// A notification that is sent when specific workflow definition versions are updated.
/// </summary>
[PublicAPI]
public record WorkflowDefinitionVersionsUpdated(IEnumerable<WorkflowDefinition> WorkflowDefinitions) : INotification;