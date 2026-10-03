using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management.Entities;

namespace Crest.Workflows.Management.Notifications;

/// <summary>
/// A notification that is sent when a workflow instance is saved.
/// </summary>
/// <param name="WorkflowInstance">The workflow instance.</param>
public record WorkflowInstanceSaved(WorkflowInstance WorkflowInstance) : INotification;