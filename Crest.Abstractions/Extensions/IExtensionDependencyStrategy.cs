using Crest.Environment.Extensions.Features;

namespace Crest.Environment.Extensions;

public interface IExtensionDependencyStrategy
{
    bool HasDependency(IFeatureInfo observer, IFeatureInfo subject);
}
