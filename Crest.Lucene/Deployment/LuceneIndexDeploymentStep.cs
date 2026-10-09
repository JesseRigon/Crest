using Microsoft.Extensions.Localization;
using Crest.Deployment;

namespace Crest.Lucene.Deployment;

/// <summary>
/// Adds layers to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class LuceneIndexDeploymentStep : DeploymentStep
{
    public LuceneIndexDeploymentStep()
    {
        Name = "LuceneIndex";
    }

    public LuceneIndexDeploymentStep(IStringLocalizer<LuceneIndexDeploymentStep> S)
        : this()
    {
        Category = S["Search"];
    }

    public bool IncludeAll { get; set; } = true;

    public string[] IndexNames { get; set; }
}
