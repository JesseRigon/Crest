using System.Text.Json.Nodes;
using Crest.Deployment;
using Crest.Search.Models;
using Crest.Settings;

namespace Crest.Search.Deployment;

public sealed class SearchSettingsDeploymentSource
    : DeploymentSourceBase<SearchSettingsDeploymentStep>
{
    private readonly ISiteService _siteService;

    public SearchSettingsDeploymentSource(ISiteService site)
    {
        _siteService = site;
    }

    protected override async Task ProcessAsync(SearchSettingsDeploymentStep step, DeploymentPlanResult result)
    {
        var searchSettings = await _siteService.GetSettingsAsync<SearchSettings>();

        result.Steps.Add(new JsonObject
        {
            ["name"] = "Settings",
            ["SearchSettings"] = JObject.FromObject(searchSettings),
        });
    }
}
