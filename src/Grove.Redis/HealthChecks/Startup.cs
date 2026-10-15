using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;

namespace Grove.Redis.HealthChecks;

[RequireFeatures("Grove.HealthChecks")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddRedisCheck();
    }
}
