using Grove.Workflows.Activities;
using Grove.Workflows.Runtime.Entities;

namespace Grove.Workflows.Runtime.Notifications;

/// <summary>
/// Represents a collection of indexed workflow triggers.
/// </summary>
public record IndexedWorkflowTriggers(Workflow Workflow, ICollection<StoredTrigger> AddedTriggers, ICollection<StoredTrigger> RemovedTriggers, ICollection<StoredTrigger> UnchangedTriggers);