using Crest.DisplayManagement.Extensions;
using Crest.Environment.Extensions.Features;

namespace Crest.DisplayManagement.Events;

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
