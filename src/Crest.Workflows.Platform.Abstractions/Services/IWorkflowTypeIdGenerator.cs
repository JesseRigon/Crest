using Crest.Workflows.Platform.Models;

namespace Crest.Workflows.Platform.Services;

public interface IWorkflowTypeIdGenerator
{
    string GenerateUniqueId(WorkflowType workflowType);
}
