using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;
using Crest.Users.Events;
using Crest.Users.Handlers;
using Crest.Users.Workflows.Activities;
using Crest.Users.Workflows.Handlers;
using Crest.Workflows.Platform.Helpers;

namespace Crest.Users.Workflows;

[RequireFeatures("Crest.Workflows")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<UserCreatedEvent>();
        services.AddActivity<UserDeletedEvent>();
        services.AddActivity<UserEnabledEvent>();
        services.AddActivity<UserDisabledEvent>();
        services.AddActivity<UserUpdatedEvent>();
        services.AddActivity<UserLoggedInEvent>();
        services.AddActivity<UserLoggedOutEvent>();
        services.AddScoped<IUserEventHandler, UserEventHandler>();
        services.AddScoped<ILogoutFormEvent, LogoutFormEventHandler>();
        services.AddActivity<AssignUserRoleTask>();
        services.AddActivity<ValidateUserTask>();
        services.AddActivity<UserConfirmedEvent>();
    }
}

[RequireFeatures("Crest.Workflows", "Crest.Email")]
public sealed class EmailWorkflowStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<RegisterUserTask>();
    }
}
