using Crest.Entities;

namespace Crest.Workflows.Platform.Services;

using Crest.Workflows.Platform.Models;

public class WorkflowTypeIdGenerator : IWorkflowTypeIdGenerator
{
    private readonly IIdGenerator _idGenerator;

    public WorkflowTypeIdGenerator(IIdGenerator idGenerator)
    {
        _idGenerator = idGenerator;
    }

    public string GenerateUniqueId(WorkflowType workflowType)
    {
        return _idGenerator.GenerateUniqueId();
    }
}
