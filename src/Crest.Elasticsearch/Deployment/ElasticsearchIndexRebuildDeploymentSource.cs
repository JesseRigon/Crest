using System.Text.Json.Nodes;
using Crest.Deployment;

namespace Crest.Elasticsearch.Deployment;

public sealed class ElasticsearchIndexRebuildDeploymentSource
    : DeploymentSourceBase<ElasticsearchIndexRebuildDeploymentStep>
{
    protected override Task ProcessAsync(ElasticsearchIndexRebuildDeploymentStep step, DeploymentPlanResult result)
    {
        var indicesToRebuild = step.IncludeAll ? [] : step.Indices;

        result.Steps.Add(new JsonObject
        {
            ["name"] = "elastic-index-rebuild",
            ["includeAll"] = step.IncludeAll,
            ["Indices"] = JArray.FromObject(indicesToRebuild),
        });

        return Task.CompletedTask;
    }
}
