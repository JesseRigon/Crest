using Crest.Workflows.Extensions;
using Crest.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Crest.Workflows.Queries.UI;
using OrchardCore.Modules;

namespace Crest.Workflows.Queries;

public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureCrestWorkflows(crestWorkflows =>
        {
            crestWorkflows.AddActivitiesFrom<Startup>();
        });

        services.AddScoped<IPropertyUIHandler, SqlCodeOptionsProvider>();
    }
}