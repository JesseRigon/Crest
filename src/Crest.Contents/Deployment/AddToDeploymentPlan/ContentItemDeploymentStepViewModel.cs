using System.ComponentModel.DataAnnotations;

namespace Crest.Contents.Deployment.AddToDeploymentPlan;

public class ContentItemDeploymentStepViewModel
{
    [Required]
    public string ContentItemId { get; set; }
}
