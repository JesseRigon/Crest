using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;

namespace Crest.Queries.Deployment;

public sealed class AllQueriesDeploymentStepDriver : DisplayDriver<DeploymentStep, AllQueriesDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(AllQueriesDeploymentStep step, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AllQueriesDeploymentStep_Summary", step).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("AllQueriesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(AllQueriesDeploymentStep step, BuildEditorContext context)
    {
        return View("AllQueriesDeploymentStep_Edit", step).Location("Content");
    }
}
