using Microsoft.Extensions.Localization;
using Grove.Deployment;

namespace Grove.Data.Queries.Deployment;

/// <summary>
/// Adds all queries to a <see cref="DeploymentPlanResult"/>.
/// </summary>
public class AllQueriesDeploymentStep : DeploymentStep
{
    public AllQueriesDeploymentStep()
    {
        Name = "AllQueries";
    }

    public AllQueriesDeploymentStep(IStringLocalizer<AllQueriesDeploymentStep> S)
        : this()
    {
        Category = S["Content Management"];
    }
}
