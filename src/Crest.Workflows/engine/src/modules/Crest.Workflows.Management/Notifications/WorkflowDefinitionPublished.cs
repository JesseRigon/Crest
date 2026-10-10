using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management.Entities;
using Crest.Workflows.Management.Models;
using JetBrains.Annotations;

namespace Crest.Workflows.Management.Notifications;

/// <summary>
/// A notification that is sent when a workflow definition is published.
/// </summary>
/// <param name="WorkflowDefinition">The workflow definition.</param>
/// <param name="AffectedWorkflows">The affected workflows.</param>
[PublicAPI]
public record WorkflowDefinitionPublished(WorkflowDefinition WorkflowDefinition, AffectedWorkflows AffectedWorkflows) : INotification;