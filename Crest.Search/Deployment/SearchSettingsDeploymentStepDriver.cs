using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.Search.Deployment;

public sealed class SearchSettingsDeploymentStepDriver : DisplayDriver<DeploymentStep, SearchSettingsDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(SearchSettingsDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("SearchSettingsDeploymentStep_Fields_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("SearchSettingsDeploymentStep_Fields_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(SearchSettingsDeploymentStep step, BuildEditorContext context)
    {
        return View("SearchSettingsDeploymentStep_Fields_Edit", step).Location("Content");
    }
}
