using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.Users.Deployment;

public class CustomUserSettingsDeploymentStep : DeploymentStep
{
    public CustomUserSettingsDeploymentStep()
    {
        Name = "CustomUserSettings";
    }

    public CustomUserSettingsDeploymentStep(IStringLocalizer<CustomUserSettingsDeploymentStep> S)
        : this()
    {
        Category = S["Security"];
    }

    public bool IncludeAll { get; set; } = true;

    public string[] SettingsTypeNames { get; set; }
}
