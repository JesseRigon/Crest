using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.DataLocalization.Deployment;

public class AllDataTranslationsDeploymentStep : DeploymentStep
{
    public AllDataTranslationsDeploymentStep()
    {
        Name = "AllDataTranslations";
    }

    public AllDataTranslationsDeploymentStep(IStringLocalizer<AllDataTranslationsDeploymentStep> S)
        : this()
    {
        Category = S["Internationalization"];
    }
}
