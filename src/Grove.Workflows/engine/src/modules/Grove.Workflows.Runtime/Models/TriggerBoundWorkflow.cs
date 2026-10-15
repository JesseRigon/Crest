using Grove.Workflows.Models;
using Grove.Workflows.Runtime.Entities;

namespace Grove.Workflows.Runtime;

/// <summary>
/// Represents a workflow bound to one or more triggers.
/// </summary>
public record TriggerBoundWorkflow(WorkflowGraph WorkflowGraph, ICollection<StoredTrigger> Triggers);