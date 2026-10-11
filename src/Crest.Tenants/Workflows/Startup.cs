using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;
using Crest.Workflows;
using Crest.Workflows.Extensions;

namespace Crest.Tenants.Workflows;

[RequireFeatures(WorkflowsConstants.FeatureId)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkflowActivityProvider, TenantsWorkflowProvider>();
        services.ConfigureCrestWorkflows(workflows => workflows.AddActivitiesFrom<Startup>());
    }
}
