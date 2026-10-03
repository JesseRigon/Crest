using Crest.Workflows.Extensions;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace Crest.Workflows.Data;

[Feature("Crest.Workflows.Data.Csv")]
public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureCrestWorkflows(crestWorkflows =>
        {
            crestWorkflows.UseCsv();
        });
    }
}