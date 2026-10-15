using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Runtime.Models;

namespace Grove.Workflows.Runtime.Notifications;

/// <summary>
/// Published when workflow definitions have been reloaded.
/// </summary>
public record WorkflowDefinitionsReloaded(ICollection<ReloadedWorkflowDefinition> ReloadedWorkflowDefinitions) : INotification;