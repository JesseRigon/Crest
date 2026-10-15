using Microsoft.Extensions.DependencyInjection;
using Grove.Modules;
using Grove.Users.Events;
using Grove.Users.Handlers;
using Grove.Users.Workflows.Handlers;
using Grove.Workflows;
using Grove.Workflows.Extensions;

namespace Grove.Users.Workflows;

[RequireFeatures(WorkflowsConstants.FeatureId)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Registered with the workflow registry: the user triggers, raised from the event
        // handlers, and the user activities.
        services.AddScoped<IWorkflowTriggerProvider, UsersWorkflowProvider>();
        services.AddScoped<IWorkflowActivityProvider, UsersWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<Startup>());

        services.AddScoped<IUserEventHandler, UserEventHandler>();
        services.AddScoped<ILogoutFormEvent, LogoutFormEventHandler>();
    }
}
