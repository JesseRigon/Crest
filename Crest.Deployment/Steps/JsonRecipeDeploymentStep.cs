using Microsoft.Extensions.Localization;

namespace Crest.Deployment.Steps;

/// <summary>
/// Adds a JSON recipe to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class JsonRecipeDeploymentStep : DeploymentStep
{
    public JsonRecipeDeploymentStep()
    {
        Name = "JsonRecipe";
    }

    public JsonRecipeDeploymentStep(IStringLocalizer<JsonRecipeDeploymentStep> S)
        : this()
    {
        Category = S["Deployment"];
    }

    public string Json { get; set; }
}
