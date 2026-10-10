using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;

namespace Crest.Sms.HealthChecks;

[RequireFeatures("Crest.HealthChecks")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddSmsCheck();
    }
}
