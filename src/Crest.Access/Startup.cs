using Crest.Access.AuditTrail;
using Crest.Access.Services;
using Crest.AuditTrail.Services.Models;
using Crest.BackgroundTasks;
using Crest.Modules;
using Crest.Security;
using Crest.Users.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Crest.Access;

public sealed class Startup : StartupBase
{
    // Before every module that registers an IAuthorizationHandler, so the one decision handler
    // is first in the handler list; order among handlers does not change the outcome, but it
    // keeps the common case (a plain permission) answered without running the rest.
    public override int Order => PlatformConstants.ConfigureOrder.InfrastructureService;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICallerContextAccessor, CallerContextAccessor>();
        services.AddScoped<ICallerContextFactory, CallerContextFactory>();
        services.AddScoped<IAccessDecision, AccessDecisionService>();
        services.AddScoped<IScopeSetProvider, ScopeSetProvider>();
        services.AddScoped<IAccessRunner, AccessRunner>();
        services.AddScoped<IAccessAuditor, AccessAuditor>();
        services.AddScoped<IPermissionVersion, PermissionVersionService>();
        services.AddSingleton<CallerStateCache>();

        services.AddScoped<IAuthorizationHandler, AccessAuthorizationHandler>();

        // Every role, binding and policy write bumps the permission version (the caller state
        // cache and every compiled scope key on it).
        services.AddScoped<IRoleCreatedEventHandler, PermissionVersionBumper>();
        services.AddScoped<IRoleUpdatedEventHandler, PermissionVersionBumper>();
        services.AddScoped<IRoleRemovedEventHandler, PermissionVersionBumper>();
        services.AddScoped<IUserEventHandler, PermissionVersionBumper>();

        // Background entry points enter the path as the system actor.
        services.AddScoped<IBackgroundTaskEventHandler, SystemCallerForBackgroundTasks>();

        services.AddTransient<IConfigureOptions<AuditTrailOptions>, AccessAuditTrailEventConfiguration>();
        services.Configure<AccessAuditOptions>(options => { });
    }
}
