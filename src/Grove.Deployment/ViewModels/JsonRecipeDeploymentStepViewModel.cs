using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Grove.Deployment.ViewModels;

public class JsonRecipeDeploymentStepViewModel
{
    public string Json { get; set; }

    [BindNever]
    public string Schema { get; set; }
}
