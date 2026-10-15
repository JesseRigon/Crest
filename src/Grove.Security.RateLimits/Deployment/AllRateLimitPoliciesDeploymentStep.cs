using Microsoft.Extensions.Localization;
using Grove.Deployment;
using Grove.Security.RateLimits.Recipes;

namespace Grove.Security.RateLimits.Deployment;

/// <summary>
/// Represents a deployment step that exports all stored rate-limit policies.
/// </summary>
public sealed class AllRateLimitPoliciesDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AllRateLimitPoliciesDeploymentStep"/> class.
    /// </summary>
    public AllRateLimitPoliciesDeploymentStep()
    {
        Name = CreateOrUpdateRateLimitPoliciesStep.StepKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AllRateLimitPoliciesDeploymentStep"/> class.
    /// </summary>
    /// <param name="S">The localizer used to assign the deployment category.</param>
    public AllRateLimitPoliciesDeploymentStep(IStringLocalizer<AllRateLimitPoliciesDeploymentStep> S)
        : this()
    {
        Category = S["Security"];
    }
}
