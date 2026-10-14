using Crest.Deployment;
using Crest.Routing;

namespace Crest.Contents.Deployment.ExportContentToDeploymentTarget;

/// <summary>
/// The deployment target settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class ExportContentToDeploymentTargetPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("deployment-targets", "Deployment Targets", "settings", ExportContentToDeploymentTargetSettingsDisplayDriver.GroupId, [DeploymentPermissions.ManageDeploymentPlan.Name]);
    }
}
