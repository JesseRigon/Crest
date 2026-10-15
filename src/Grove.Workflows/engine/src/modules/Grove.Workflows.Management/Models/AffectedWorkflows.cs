using Grove.Workflows.Management.Entities;

namespace Grove.Workflows.Management.Models;

public record AffectedWorkflows(ICollection<WorkflowDefinition> WorkflowDefinitions);