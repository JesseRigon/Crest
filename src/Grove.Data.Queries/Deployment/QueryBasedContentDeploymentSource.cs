using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Grove.Contents.Management;
using Grove.Deployment;

namespace Grove.Data.Queries.Deployment;

public sealed class QueryBasedContentDeploymentSource
    : DeploymentSourceBase<QueryBasedContentDeploymentStep>
{
    private readonly IQueryManager _queryManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public QueryBasedContentDeploymentSource(IQueryManager queryManager, IHttpContextAccessor httpContextAccessor)
    {
        _queryManager = queryManager;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task ProcessAsync(QueryBasedContentDeploymentStep step, DeploymentPlanResult result)
    {
        var data = new JsonArray();

        var query = await _queryManager.GetQueryAsync(step.QueryName);

        if (query == null)
        {
            return;
        }

        if (!query.ReturnContentItems)
        {
            return;
        }

        if (!TryDeserializeParameters(step.QueryParameters ?? "{ }", out var parameters))
        {
            return;
        }

        var cancellationToken = _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

        // Every page of the query, so the export is complete; a source that does not page
        // hands back no token after its one page.
        string pageToken = null;

        do
        {
            var results = await _queryManager.ExecuteQueryAsync(query, new QueryRequest
            {
                Parameters = parameters,
                PageToken = pageToken,
                CancellationToken = cancellationToken,
            });

            AddItems(step, results.Items, data);
            pageToken = results.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));

        if (data.HasValues())
        {
            var jObj = new JsonObject
            {
                ["name"] = "content",
                ["data"] = data,
            };

            result.Steps.Add(jObj);
        }
    }

    private static void AddItems(QueryBasedContentDeploymentStep step, IEnumerable<object> items, JsonArray data)
    {
        foreach (var contentItem in items)
        {
            var objectData = JObject.FromObject(contentItem);

            // Don't serialize the Id as it could be interpreted as an updated object when added back to YesSql.
            objectData.Remove(nameof(ContentItem.Id));

            if (step.ExportAsSetupRecipe)
            {
                objectData[nameof(ContentItem.Owner)] = "[js: parameters('AdminUserId')]";
                objectData[nameof(ContentItem.Author)] = "[js: parameters('AdminUsername')]";
                objectData[nameof(ContentItem.ContentItemId)] = "[js: uuid()]";
                objectData.Remove(nameof(ContentItem.ContentItemVersionId));
                objectData.Remove(nameof(ContentItem.CreatedUtc));
                objectData.Remove(nameof(ContentItem.ModifiedUtc));
                objectData.Remove(nameof(ContentItem.PublishedUtc));
            }
            data.Add(objectData);
        }
    }

    private static bool TryDeserializeParameters(string parameters, out Dictionary<string, object> queryParameters)
    {
        try
        {
            queryParameters = JConvert.DeserializeObject<Dictionary<string, object>>(parameters) ?? [];

            return true;
        }
        catch (JsonException)
        {
            queryParameters = [];

            return false;
        }
    }
}
