using Crest.Environment.Extensions;
using Crest.Environment.Extensions.Features;

namespace Crest.DisplayManagement.Extensions;

public class ThemeExtensionDependencyStrategy : IExtensionDependencyStrategy
{
    public bool HasDependency(IFeatureInfo observer, IFeatureInfo subject)
    {
        if (observer.IsTheme())
        {
            if (!subject.IsTheme())
            {
                return true;
            }
        }

        return false;
    }
}
