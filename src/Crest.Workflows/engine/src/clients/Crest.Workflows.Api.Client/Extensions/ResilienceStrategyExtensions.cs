using System.Text.Json.Nodes;

namespace Crest.Workflows.Api.Client.Extensions;

public static class ResilienceStrategyExtensions
{
    public static string GetResilienceStrategyId(this JsonObject strategyJsonObject)
    {
        return strategyJsonObject.GetProperty<string>("id")!;
    }
    
    public static string GetResilienceStrategyDisplayName(this JsonObject strategyJsonObject)
    {
        return strategyJsonObject.GetProperty<string>("displayName")!;
    }
}