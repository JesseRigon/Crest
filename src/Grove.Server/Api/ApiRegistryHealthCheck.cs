using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Grove.Api;

/// <summary>
/// Reports the tenant's API contributors that were left out because their endpoints could not be
/// built (<see cref="IApiEndpointRegistry.Failures"/>): degraded, with each contributor's error.
/// </summary>
internal sealed class ApiRegistryHealthCheck(IApiEndpointRegistry registry) : IHealthCheck
{
    public const string Name = "api-registry";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var failures = registry.Failures;
        if (failures.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy($"{registry.Declared.Count} API contributors mapped."));
        }

        var data = failures.ToDictionary(failure => failure.Name, failure => (object)failure.Error, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(HealthCheckResult.Degraded(
            $"{failures.Count} API contributor(s) left out: {string.Join(", ", failures.Select(failure => failure.Name))}.",
            failures[0].Exception,
            data));
    }
}
