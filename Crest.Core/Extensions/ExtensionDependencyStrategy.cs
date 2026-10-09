using Crest.Environment.Extensions.Features;

namespace Crest.Environment.Extensions;

public class ExtensionDependencyStrategy : IExtensionDependencyStrategy
{
    public bool HasDependency(IFeatureInfo observer, IFeatureInfo subject)
    {
        return observer.Dependencies.Contains(subject.Id);
    }
}
