using Crest.Environment.Extensions.Features;

namespace Crest.Environment.Extensions;

public interface IExtensionPriorityStrategy
{
    int GetPriority(IFeatureInfo feature);
}
