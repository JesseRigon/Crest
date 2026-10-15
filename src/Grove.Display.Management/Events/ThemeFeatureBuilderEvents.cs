using Grove.Display.Management.Extensions;
using Grove.Environment.Extensions.Features;

namespace Grove.Display.Management.Events;

public class ThemeFeatureBuilderEvents : FeatureBuilderEvents
{
    public override void Building(FeatureBuildingContext context)
    {
        if (context.ExtensionInfo.Manifest.IsTheme())
        {
            var extensionInfo = new ThemeExtensionInfo(context.ExtensionInfo);

            if (extensionInfo.HasBaseTheme() && context.FeatureId == context.ExtensionInfo.Id)
            {
                context.FeatureDependencyIds = context
                    .FeatureDependencyIds
                    .Concat(new[] { extensionInfo.BaseTheme })
                    .ToArray();
            }

            context.ExtensionInfo = extensionInfo;
        }
    }
}
