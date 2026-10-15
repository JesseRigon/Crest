using Grove.Environment.Extensions.Features;

namespace Grove.Environment.Extensions;

public interface IExtensionPriorityStrategy
{
    int GetPriority(IFeatureInfo feature);
}
