using System.ComponentModel.DataAnnotations;
using Grove.Settings;

namespace Grove.Contents.Deployment.ExportContentToDeploymentTarget;

public class ExportContentToDeploymentTargetSettings
{
    public const string GroupId = "ExportContentToDeploymentTarget";

    /// <summary>
    /// The id of the export content to deployment target deployment plan.
    /// </summary>
    [Display(Name = "Deployment plan", Description = "The plan whose \"Export content to deployment target\" step content items are exported with. Create a plan with that step first.")]
    [Setting(Options = typeof(ExportContentToDeploymentTargetPlanOptions))]
    public long ExportContentToDeploymentTargetPlanId { get; set; }
}
