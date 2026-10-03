using Crest.Workflows.Management.Entities;

namespace Crest.Workflows.Management.Models;

public record AffectedWorkflows(ICollection<WorkflowDefinition> WorkflowDefinitions);