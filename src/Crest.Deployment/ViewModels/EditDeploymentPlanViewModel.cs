using System.ComponentModel.DataAnnotations;

namespace Crest.Deployment.ViewModels;

public class EditDeploymentPlanViewModel
{
    public long Id { get; set; }

    [Required]
    public string Name { get; set; }
}
