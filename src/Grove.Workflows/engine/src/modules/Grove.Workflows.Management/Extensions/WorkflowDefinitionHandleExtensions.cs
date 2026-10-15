using Grove.Workflows.Management.Filters;
using Grove.Workflows.Models;

namespace Grove.Workflows.Management;

public static class WorkflowDefinitionHandleExtensions
{
    public static WorkflowDefinitionFilter ToFilter(this WorkflowDefinitionHandle handle)
    {
        return new()
        {
            DefinitionId = handle.DefinitionId,
            Id = handle.DefinitionVersionId,
            VersionOptions = handle.VersionOptions
        };
    }
}