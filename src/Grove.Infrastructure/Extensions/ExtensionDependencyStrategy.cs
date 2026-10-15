using Grove.Environment.Extensions.Features;

namespace Grove.Environment.Extensions;

public class ExtensionDependencyStrategy : IExtensionDependencyStrategy
{
    public bool HasDependency(IFeatureInfo observer, IFeatureInfo subject)
    {
        return observer.Dependencies.Contains(subject.Id);
    }
}
