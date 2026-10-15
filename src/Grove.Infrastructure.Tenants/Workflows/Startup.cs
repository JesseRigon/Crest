using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;
using Grove.Workflows;
using Grove.Workflows.Extensions;

namespace Grove.Infrastructure.Tenants.Workflows;

[RequireFeatures(WorkflowsConstants.FeatureId)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkflowActivityProvider, TenantsWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<Startup>());
    }
}
