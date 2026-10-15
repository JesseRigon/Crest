using Grove.Environment.Extensions.Features;

namespace Grove.Environment.Extensions;

public class ExtensionPriorityStrategy : IExtensionPriorityStrategy
{
    public int GetPriority(IFeatureInfo feature)
    {
        return feature.Priority;
    }
}
