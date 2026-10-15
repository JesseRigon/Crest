using System.ComponentModel.DataAnnotations;

namespace Grove.Contents.Deployment.AddToDeploymentPlan;

public class ContentItemDeploymentStepViewModel
{
    [Required]
    public string ContentItemId { get; set; }
}
