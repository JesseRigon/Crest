using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Grove.Contents.Management;
using Grove.Deployment;

namespace Grove.Contents.Deployment.ExportContentToDeploymentTarget;

public sealed class ExportContentToDeploymentTargetDeploymentSource
    : DeploymentSourceBase<ExportContentToDeploymentTargetDeploymentStep>
{
    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ExportContentToDeploymentTargetDeploymentSource(
        IContentManager contentManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _contentManager = contentManager;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task ProcessAsync(ExportContentToDeploymentTargetDeploymentStep step, DeploymentPlanResult result)
    {
        var data = new JsonArray();
        result.Steps.Add(new JsonObject
        {
            ["name"] = "Content",
            ["data"] = data,
        });

        // The content list's API calls (a plan's package or its export to a remote instance) name
        // the items in the query string: ?contentItemId=a&contentItemId=b&latest=true.
        var query = _httpContextAccessor.HttpContext?.Request.Query;
        if (query is null)
        {
            return;
        }

        var latest = bool.TryParse(query["latest"], out var isLatest) && isLatest;
        foreach (var contentItemId in query["contentItemId"].Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
        {
            var contentItem = await _contentManager.GetAsync(contentItemId, latest ? VersionOptions.Latest : VersionOptions.Published);
            if (contentItem != null)
            {
                var objectData = JObject.FromObject(contentItem);
                objectData.Remove(nameof(ContentItem.Id));
                data.Add(objectData);
            }
        }
    }
}
