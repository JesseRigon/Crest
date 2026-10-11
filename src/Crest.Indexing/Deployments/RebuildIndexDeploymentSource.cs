using System.Text.Json.Nodes;
using Crest.Deployment;
using Crest.Indexing.Recipes;

namespace Crest.Indexing.Deployments;

public sealed class RebuildIndexDeploymentSource
    : DeploymentSourceBase<RebuildIndexDeploymentStep>
{
    protected override Task ProcessAsync(RebuildIndexDeploymentStep step, DeploymentPlanResult result)
    {
        var indicesToRebuild = step.IncludeAll
            ? []
            : step.IndexNames ?? [];

        result.Steps.Add(new JsonObject
        {
            ["name"] = RebuildIndexStep.Key,
            ["includeAll"] = step.IncludeAll,
            ["indexNames"] = JArray.FromObject(indicesToRebuild),
        });

        return Task.CompletedTask;
    }
}
