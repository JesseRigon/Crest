using Grove.Environment.Extensions;
using Grove.Environment.Extensions.Features;

namespace Grove.Display.Management.Extensions;

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
