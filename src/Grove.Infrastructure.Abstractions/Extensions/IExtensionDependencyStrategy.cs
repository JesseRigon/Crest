using Grove.Environment.Extensions.Features;

namespace Grove.Environment.Extensions;

public interface IExtensionDependencyStrategy
{
    bool HasDependency(IFeatureInfo observer, IFeatureInfo subject);
}
