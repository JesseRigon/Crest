using Crest.Workflows.Management.Filters;
using Crest.Workflows.Models;

namespace Crest.Workflows.Management;

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