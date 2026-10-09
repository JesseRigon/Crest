using Microsoft.Extensions.Localization;
using Crest.Deployment;

namespace Crest.DataLocalization.Deployment;

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
