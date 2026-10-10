using Crest.Entities;

namespace Crest.Workflows.Platform.Services;

using Crest.Workflows.Platform.Models;

public class WorkflowIdGenerator : IWorkflowIdGenerator
{
    private readonly IIdGenerator _idGenerator;

    public WorkflowIdGenerator(IIdGenerator idGenerator)
    {
        _idGenerator = idGenerator;
    }

    public string GenerateUniqueId(Workflow workflow)
    {
        return _idGenerator.GenerateUniqueId();
    }
}
