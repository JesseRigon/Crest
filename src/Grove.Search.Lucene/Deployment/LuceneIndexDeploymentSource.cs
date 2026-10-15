using System.Text.Json.Nodes;
using Grove.Deployment;
using Grove.Data.Indexing;
using Grove.Data.Indexing.Models;

namespace Grove.Search.Lucene.Deployment;

public sealed class LuceneIndexDeploymentSource
    : DeploymentSourceBase<LuceneIndexDeploymentStep>
{
    private readonly IIndexProfileStore _indexStore;

    public LuceneIndexDeploymentSource(IIndexProfileStore indexStore)
    {
        _indexStore = indexStore;
    }

    protected override async Task ProcessAsync(LuceneIndexDeploymentStep step, DeploymentPlanResult result)
    {
        var indexSettings = await _indexStore.GetByProviderAsync(LuceneConstants.ProviderName);

        var data = new JsonArray();
        var indicesToAdd = step.IncludeAll
            ? indexSettings.Select(x => x.IndexName).ToArray()
            : step.IndexNames;

        foreach (var index in indexSettings)
        {
            if (indicesToAdd.Contains(index.IndexName))
            {
                var indexSettingsDict = new Dictionary<string, IndexProfile>
                {
                    { index.IndexName, index },
                };

                data.Add(JObject.FromObject(indexSettingsDict));
            }
        }

        // Adding Lucene settings
        result.Steps.Add(new JsonObject
        {
            ["name"] = "lucene-index",
            ["Indices"] = data,
        });
    }
}
