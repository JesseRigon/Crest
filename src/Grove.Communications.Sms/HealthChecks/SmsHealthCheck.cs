using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Grove.Communications.Sms.HealthChecks;

/// <summary>
/// The SMS channel is healthy when a default provider resolves. Whether a provider's service
/// answers is its connection's concern (the Connections screen tests a connection).
/// </summary>
internal sealed class SmsHealthCheck : IHealthCheck
{
    private readonly ISmsProviderResolver _providerResolver;

    public SmsHealthCheck(ISmsProviderResolver providerResolver)
    {
        _providerResolver = providerResolver;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _providerResolver.GetAsync() is null
                ? HealthCheckResult.Unhealthy(description: "No SMS provider is enabled.")
                : HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(description: "Resolving the SMS provider failed.", exception);
        }
    }
}
