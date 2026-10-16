using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Deployment;

namespace Grove.Contents.Types.Deployment;

public sealed class ReplaceContentDefinitionDeploymentSource
    : DeploymentSourceBase<ReplaceContentDefinitionDeploymentStep>
{
    private readonly IContentDefinitionStore _contentDefinitionStore;

    public ReplaceContentDefinitionDeploymentSource(IContentDefinitionStore contentDefinitionStore)
    {
        _contentDefinitionStore = contentDefinitionStore;
    }

    protected override async Task ProcessAsync(ReplaceContentDefinitionDeploymentStep step, DeploymentPlanResult result)
    {
        var contentTypeDefinitionRecord = await _contentDefinitionStore.LoadContentDefinitionAsync();

        var contentTypes = step.IncludeAll
            ? contentTypeDefinitionRecord.ContentTypeDefinitionRecords
            : contentTypeDefinitionRecord.ContentTypeDefinitionRecords
                .Where(x => step.ContentTypes.Contains(x.Name));

        var contentFieldSets = step.IncludeAll
            ? contentTypeDefinitionRecord.ContentFieldSetDefinitionRecords
            : contentTypeDefinitionRecord.ContentFieldSetDefinitionRecords
                    .Where(x => step.ContentFieldSets.Contains(x.Name));

        result.Steps.Add(new JsonObject
        {
            ["name"] = "ReplaceContentDefinition",
            ["ContentTypes"] = JArray.FromObject(contentTypes),
            ["ContentFieldSets"] = JArray.FromObject(contentFieldSets),
        });
    }
}
