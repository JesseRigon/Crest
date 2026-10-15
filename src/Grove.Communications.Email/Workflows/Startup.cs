using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;
using Grove.Workflows;
using Grove.Workflows.Extensions;

namespace Grove.Communications.Email.Workflows;

[RequireFeatures(WorkflowsConstants.FeatureId)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkflowActivityProvider, EmailWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<Startup>());
    }
}
