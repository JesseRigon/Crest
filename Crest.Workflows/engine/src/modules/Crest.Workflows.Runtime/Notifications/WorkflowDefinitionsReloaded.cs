using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Models;

namespace Crest.Workflows.Runtime.Notifications;

/// <summary>
/// Published when workflow definitions have been reloaded.
/// </summary>
public record WorkflowDefinitionsReloaded(ICollection<ReloadedWorkflowDefinition> ReloadedWorkflowDefinitions) : INotification;