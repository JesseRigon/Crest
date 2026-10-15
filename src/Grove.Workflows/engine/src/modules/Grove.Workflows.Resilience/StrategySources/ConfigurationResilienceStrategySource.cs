using Grove.Workflows.Extensions;
using Grove.Workflows.Resilience.Serialization;
using Microsoft.Extensions.Configuration;

namespace Grove.Workflows.Resilience.StrategySources;

public class ConfigurationResilienceStrategySource(IConfiguration configuration, ResilienceStrategySerializer serializer) : IResilienceStrategySource
{
    public Task<IEnumerable<IResilienceStrategy>> GetStrategiesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetStrategies());
    }
    
    private IEnumerable<IResilienceStrategy> GetStrategies()
    {
        var json = configuration.GetSectionAsJson("Resilience:Strategies");
        return string.IsNullOrWhiteSpace(json) ? [] : serializer.DeserializeMany(json);
    }
}