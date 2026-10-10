using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;
using Crest.Tenants.Workflows.Activities;
using Crest.Workflows.Platform.Helpers;

namespace Crest.Tenants.Workflows;

[RequireFeatures("Crest.Workflows")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<DisableTenantTask>();
        services.AddActivity<EnableTenantTask>();
        services.AddActivity<CreateTenantTask>();
        services.AddActivity<SetupTenantTask>();
    }
}
