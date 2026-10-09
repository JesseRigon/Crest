using Microsoft.Extensions.Localization;
using Crest.Deployment;

namespace Crest.Contents.Deployment;

/// <summary>
/// Adds all content items of a specific type to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class ContentDeploymentStep : DeploymentStep
{
    public ContentDeploymentStep()
    {
        Name = "ContentDeploymentStep";
    }

    public ContentDeploymentStep(IStringLocalizer<ContentDeploymentStep> S)
        : this()
    {
        Category = S["Content Management"];
    }

    public string[] ContentTypes { get; set; }
    public bool ExportAsSetupRecipe { get; set; }
}
