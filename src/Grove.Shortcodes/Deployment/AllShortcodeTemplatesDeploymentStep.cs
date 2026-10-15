using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.Shortcodes.Deployment;

public class AllShortcodeTemplatesDeploymentStep : DeploymentStep
{
    public AllShortcodeTemplatesDeploymentStep()
    {
        Name = "AllShortcodeTemplates";
    }

    public AllShortcodeTemplatesDeploymentStep(IStringLocalizer<AllShortcodeTemplatesDeploymentStep> S)
        : this()
    {
        Category = S["Content"];
    }
}
