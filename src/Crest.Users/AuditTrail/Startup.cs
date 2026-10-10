using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.AuditTrail.Models;
using Crest.AuditTrail.Services.Models;
using Crest.Data;
using Crest.Data.Migration;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;
using Crest.Settings.Deployment;
using Crest.Users.AuditTrail.Drivers;
using Crest.Users.AuditTrail.Handlers;
using Crest.Users.AuditTrail.Indexes;
using Crest.Users.AuditTrail.Models;
using Crest.Users.AuditTrail.Security;
using Crest.Users.AuditTrail.Services;
using Crest.Users.Events;
using Crest.Users.Handlers;

namespace Crest.Users.AuditTrail;

[Feature("Crest.Users.AuditTrail")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, UserAuditTrailEventConfiguration>();

        services.AddScoped<UserEventHandler, UserEventHandler>()
            .AddScoped<IUserEventHandler>(sp => sp.GetRequiredService<UserEventHandler>())
            .AddScoped<ILoginFormEvent>(sp => sp.GetRequiredService<UserEventHandler>());

        services.AddDisplayDriver<AuditTrailEvent, AuditTrailUserEventDisplayDriver>();
        services.AddIndexProvider<AuditTrailUserEventIndexProvider>();
        services.AddDataMigration<Migrations>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<IAuthorizationHandler, ViewUserAuditTrailEventsHandler>();

        services.AddSingleton<Redactor>(_ => NullRedactor.Instance);
        services.AddSingleton<Redactor>(_ => ErasingRedactor.Instance);
        services.AddSingleton<Redactor, RemoveRedactor>();
        services.AddSingleton<Redactor, PartialAsteriskRedactor>();

        services.AddTransient<Redactor>(provider => 
            provider.GetService<IOptions<HmacRedactorOptions>>() is { Value.Key.Length: > 0 } options ? 
                new HmacRedactor(options) :
                null);
        
        services.AddPermissionProvider<Permissions>();
    }
}

[RequireFeatures("Crest.Users.AuditTrail", "Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services) =>
        services.AddSiteSettingsPropertyDeploymentStep<AuditTrailUserEventSettings, DeploymentStartup>(
            S => S["User Audit Trail settings"],
            S => S["Exports the user audit trail settings."]);
}
