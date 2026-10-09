using Crest.Workflows.Platform.Models;

namespace Crest.Workflows.Platform.Services;

public interface IWorkflowIdGenerator
{
    string GenerateUniqueId(Workflow workflow);
}
