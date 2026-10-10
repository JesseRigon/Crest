using Microsoft.Extensions.Localization;
using Crest.Deployment;

namespace Crest.ContentTypes.Deployment;

public class ReplaceContentDefinitionDeploymentStep : DeploymentStep
{
    public ReplaceContentDefinitionDeploymentStep()
    {
        Name = "ReplaceContentDefinition";
    }

    public ReplaceContentDefinitionDeploymentStep(IStringLocalizer<ReplaceContentDefinitionDeploymentStep> S)
        : this()
    {
        Category = S["Content Management"];
    }

    public bool IncludeAll { get; set; }

    public string[] ContentTypes { get; set; }

    public string[] ContentParts { get; set; }
}
