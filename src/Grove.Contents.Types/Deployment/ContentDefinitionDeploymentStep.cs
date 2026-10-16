using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.Contents.Types.Deployment;

/// <summary>
/// Adds selected content definitions to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class ContentDefinitionDeploymentStep : DeploymentStep
{
    public ContentDefinitionDeploymentStep()
    {
        Name = "ContentDefinition";
    }

    public ContentDefinitionDeploymentStep(IStringLocalizer<ContentDefinitionDeploymentStep> S)
        : this()
    {
        Category = S["Content Management"];
    }

    public bool IncludeAll { get; set; }

    public string[] ContentTypes { get; set; }

    public string[] ContentFieldSets { get; set; }
}
