using Crest.Entities;

namespace Crest.Workflows.Platform.Services;

using Crest.Workflows.Platform.Models;

public class ActivityIdGenerator : IActivityIdGenerator
{
    private readonly IIdGenerator _idGenerator;

    public ActivityIdGenerator(IIdGenerator idGenerator)
    {
        _idGenerator = idGenerator;
    }

    public string GenerateUniqueId(ActivityRecord activityRecord)
    {
        return _idGenerator.GenerateUniqueId();
    }
}
