using Grove.Workflows.Models;
using Grove.Workflows.Runtime.Entities;

namespace Grove.Workflows.Http;

/// <summary>
/// Represents the result of a workflow lookup.
/// </summary>
public record HttpWorkflowLookupResult(WorkflowGraph? WorkflowGraph, ICollection<StoredTrigger> Triggers);