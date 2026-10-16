using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Deployment;

namespace Grove.Contents.Types.Deployment;

public sealed class ContentDefinitionDeploymentSource
    : DeploymentSourceBase<ContentDefinitionDeploymentStep>
{
    private readonly IContentDefinitionStore _contentDefinitionStore;

    public ContentDefinitionDeploymentSource(IContentDefinitionStore contentDefinitionStore)
    {
        _contentDefinitionStore = contentDefinitionStore;
    }

    protected override async Task ProcessAsync(ContentDefinitionDeploymentStep step, DeploymentPlanResult result)
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
            ["name"] = "ContentDefinition",
            ["ContentTypes"] = JArray.FromObject(contentTypes),
            ["ContentFieldSets"] = JArray.FromObject(contentFieldSets),
        });
    }
}
