using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;

namespace Grove.Communications.Sms.HealthChecks;

[RequireFeatures("Grove.Infrastructure.HealthChecks")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddSmsCheck();
    }
}
