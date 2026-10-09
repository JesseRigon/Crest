using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.RateLimits.Deployment;

public sealed class AllRateLimitPoliciesDeploymentStepDriver : DisplayDriver<DeploymentStep, AllRateLimitPoliciesDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(AllRateLimitPoliciesDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AllRateLimitPoliciesDeploymentStep_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
            View("AllRateLimitPoliciesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(AllRateLimitPoliciesDeploymentStep step, BuildEditorContext context)
        => View("AllRateLimitPoliciesDeploymentStep_Edit", step).Location("Content");
}
