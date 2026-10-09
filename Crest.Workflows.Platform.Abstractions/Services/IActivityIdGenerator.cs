using Crest.Workflows.Platform.Models;

namespace Crest.Workflows.Platform.Services;

public interface IActivityIdGenerator
{
    string GenerateUniqueId(ActivityRecord activityRecord);
}
