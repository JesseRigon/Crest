using Crest.Workflows.Models;
using Crest.Workflows.Runtime.Entities;

namespace Crest.Workflows.Http;

/// <summary>
/// Represents the result of a workflow lookup.
/// </summary>
public record HttpWorkflowLookupResult(WorkflowGraph? WorkflowGraph, ICollection<StoredTrigger> Triggers);