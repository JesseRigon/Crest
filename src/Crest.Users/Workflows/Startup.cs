using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;
using Crest.Users.Events;
using Crest.Users.Handlers;
using Crest.Users.Workflows.Handlers;
using Crest.Workflows;
using Crest.Workflows.Extensions;

namespace Crest.Users.Workflows;

[RequireFeatures(WorkflowsConstants.FeatureId)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Registered with the workflow registry: the user triggers, raised from the event
        // handlers, and the user activities.
        services.AddScoped<IWorkflowTriggerProvider, UsersWorkflowProvider>();
        services.AddScoped<IWorkflowActivityProvider, UsersWorkflowProvider>();
        services.ConfigureCrestWorkflows(workflows => workflows.AddActivitiesFrom<Startup>());

        services.AddScoped<IUserEventHandler, UserEventHandler>();
        services.AddScoped<ILogoutFormEvent, LogoutFormEventHandler>();
    }
}
