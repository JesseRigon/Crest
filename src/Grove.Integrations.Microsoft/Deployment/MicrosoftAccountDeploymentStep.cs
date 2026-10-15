using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.Integrations.Microsoft.Deployment;

public sealed class MicrosoftAccountDeploymentStep : DeploymentStep
{
    public MicrosoftAccountDeploymentStep()
    {
        Name = "MicrosoftAccount";
    }

    public MicrosoftAccountDeploymentStep(IStringLocalizer<MicrosoftAccountDeploymentStep> S)
        : this()
    {
        Category = S["Microsoft Authentication"];
    }
}
