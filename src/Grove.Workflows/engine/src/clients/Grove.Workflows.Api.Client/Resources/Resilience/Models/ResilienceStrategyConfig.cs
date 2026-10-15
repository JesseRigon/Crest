using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Workflows.Api.Client.Resources.Scripting.Models;
using Grove.Workflows.Api.Client.Serialization;

namespace Grove.Workflows.Api.Client.Resources.Resilience.Models;

public class ResilienceStrategyConfig
{
    public ResilienceStrategyConfigMode Mode { get; set; }
    public string? StrategyId { get; set; }
    public Expression? Expression { get; set; }

    public JsonNode SerializeToNode()
    {
        return JsonSerializer.SerializeToNode(this, SerializerOptions.ResilienceStrategyConfigSerializerOptions)!;
    }
    
    public static ResilienceStrategyConfig? Deserialize(JsonNode? node)
    {
        return node.Deserialize<ResilienceStrategyConfig?>(SerializerOptions.ResilienceStrategyConfigSerializerOptions)!;
    }
}