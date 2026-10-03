using Crest.Workflows.Mediator.Contracts;

namespace Crest.Workflows.Runtime.Notifications;

/// <summary>
/// Published when workflow definitions have been refreshed.
/// </summary>
public record WorkflowDefinitionsRefreshed(ICollection<string> WorkflowDefinitionIds) : INotification;