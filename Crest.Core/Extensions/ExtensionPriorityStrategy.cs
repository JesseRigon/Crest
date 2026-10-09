using Crest.Environment.Extensions.Features;

namespace Crest.Environment.Extensions;

public class ExtensionPriorityStrategy : IExtensionPriorityStrategy
{
    public int GetPriority(IFeatureInfo feature)
    {
        return feature.Priority;
    }
}
