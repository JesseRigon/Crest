using Crest.Workflows.Models;
using Crest.Workflows.Runtime.Entities;

namespace Crest.Workflows.Runtime;

/// <summary>
/// Represents a workflow bound to one or more triggers.
/// </summary>
public record TriggerBoundWorkflow(WorkflowGraph WorkflowGraph, ICollection<StoredTrigger> Triggers);