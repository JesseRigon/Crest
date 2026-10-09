using Crest.Workflows.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;

namespace Crest.Workflows.UI;

public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureCrestWorkflows(crestWorkflows =>
        {
            crestWorkflows.AddActivitiesFrom<Startup>();
        });   
    }
}