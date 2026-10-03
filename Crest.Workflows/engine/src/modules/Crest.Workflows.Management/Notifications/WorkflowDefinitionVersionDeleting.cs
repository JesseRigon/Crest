using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management.Entities;
using JetBrains.Annotations;

namespace Crest.Workflows.Management.Notifications;

/// <summary>
/// A notification that is sent when a workflow definition version is about to be deleted.
/// </summary>
[PublicAPI]
public record WorkflowDefinitionVersionDeleting(WorkflowDefinition WorkflowDefinition) : INotification;