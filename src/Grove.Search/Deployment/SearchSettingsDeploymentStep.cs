using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.Search.Deployment;

/// <summary>
/// Adds layers to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class SearchSettingsDeploymentStep : DeploymentStep
{
    public SearchSettingsDeploymentStep()
    {
        Name = "SearchSettings";
    }

    public SearchSettingsDeploymentStep(IStringLocalizer<SearchSettingsDeploymentStep> S)
        : this()
    {
        Category = S["Search"];
    }
}
