using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Deployment;
using Grove.Json;
using Grove.Display.Layers.Services;
using Microsoft.Extensions.Options;

namespace Grove.Display.Layers.Deployment;

/// <summary>Exports the layers, their rules and their placements as a Layers step.</summary>
public sealed class AllLayersDeploymentSource(ILayerService layerService, IOptions<DocumentJsonSerializerOptions> serializationOptions)
    : DeploymentSourceBase<AllLayersDeploymentStep>
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = serializationOptions.Value.SerializerOptions;

    protected override async Task ProcessAsync(AllLayersDeploymentStep step, DeploymentPlanResult result)
    {
        var layers = await layerService.GetLayersAsync();
        result.Steps.Add(new JsonObject
        {
            ["name"] = "Layers",
            ["Layers"] = JArray.FromObject(layers.Layers, _jsonSerializerOptions),
            ["Placements"] = JArray.FromObject(layers.Placements, _jsonSerializerOptions),
        });
    }
}
