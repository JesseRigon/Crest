using Crest.Workflows.Extensions;
using Crest.Workflows.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;

namespace Crest.Workflows.Connectors;

/// <summary>
/// The engine's resilience feature carrying a connection's retry policy (plans/workflows.md ›
/// Connectors): one strategy, <c>connection</c>, whose parameters come from the connection
/// the calling node names - the connection's owner decides how many retries, not each flow.
/// Connector activities select it by default; a tenant may pick another strategy on a node.
/// Attempts are journaled by the engine like any resilient activity's.
/// </summary>
[ResilienceCategory("Connectors")]
public sealed class ConnectionResilienceStrategy : IResilienceStrategy
{
    public const string StrategyId = "connection";

    public string Id { get; set; } = StrategyId;
    public string DisplayName { get; set; } = "The connection's retry policy";

    public async Task ConfigurePipeline<T>(ResiliencePipelineBuilder<T> pipelineBuilder, ResilienceContext context)
    {
        if (typeof(T) != typeof(ConnectorResponse))
        {
            throw new NotSupportedException($"{nameof(ConnectionResilienceStrategy)} only applies to connector calls.");
        }

        if (!context.Properties.TryGetValue(new ResiliencePropertyKey<ActivityExecutionContext>(nameof(ActivityExecutionContext)), out var activityContext)
            || activityContext.Activity is not ConnectorActivityBase connector)
        {
            return;
        }

        var connection = await activityContext.GetRequiredService<WorkflowConnectionService>().FindAsync(connector.Connection.GetOrDefault(activityContext));
        var retries = Math.Clamp(connection?.RetryCount ?? 0, 0, 5);
        if (retries == 0)
        {
            return;
        }

        pipelineBuilder.AddRetry(new RetryStrategyOptions<T>
        {
            ShouldHandle = new PredicateBuilder<T>().HandleResult(response => ((ConnectorResponse)(object)response!).Transient),
            MaxRetryAttempts = retries,
            Delay = TimeSpan.FromMilliseconds(200),
            BackoffType = DelayBackoffType.Exponential,
            MaxDelay = TimeSpan.FromSeconds(2),
            Name = $"connection {connection!.Key}",
        });
    }
}

/// <summary>Publishes the connection strategy to the engine's catalog (the designer's strategy picker and the node's default).</summary>
public sealed class ConnectionResilienceStrategySource : IResilienceStrategySource
{
    public Task<IEnumerable<IResilienceStrategy>> GetStrategiesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<IResilienceStrategy>>([new ConnectionResilienceStrategy()]);
}
