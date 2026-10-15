using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services.Models;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Display.Management.Handlers;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;
using Grove.Infrastructure.Settings.Deployment;
using Grove.Access.Users.AuditTrail.Handlers;
using Grove.Access.Users.AuditTrail.Indexes;
using Grove.Access.Users.AuditTrail.Models;
using Grove.Access.Users.AuditTrail.Security;
using Grove.Access.Users.AuditTrail.Services;
using Grove.Access.Users.Events;
using Grove.Access.Users.Handlers;

namespace Grove.Access.Users.AuditTrail;

[Feature("Grove.Access.Users.AuditTrail")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, UserAuditTrailEventConfiguration>();

        services.AddScoped<UserEventHandler, UserEventHandler>()
            .AddScoped<IUserEventHandler>(sp => sp.GetRequiredService<UserEventHandler>())
            .AddScoped<ILoginFormEvent>(sp => sp.GetRequiredService<UserEventHandler>())
            .AddScoped<ILogoutFormEvent>(sp => sp.GetRequiredService<UserEventHandler>());

        services.AddIndexProvider<AuditTrailUserEventIndexProvider>();
        services.AddDataMigration<Migrations>();
        services.AddSettingsGroup<UserSnapshotSettings>(
            UserSnapshotSettings.GroupId,
            "User Audit Trail",
            Permissions.ManageUserAuditTrailSettings,
            group => group
                .InMenu("settings-security", entryId: UserSnapshotSettings.GroupId)
                .StoredBy(
                    (services, _) => UserSnapshotSettings.LoadAsync(services),
                    (services, settings, _) => UserSnapshotSettings.SaveAsync(services, settings)));
        services.AddScoped<Grove.Access.IResourcePermissionMapper, UserAuditTrailPermissionMapper>();

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

[RequireFeatures("Grove.Access.Users.AuditTrail", "Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services) =>
        services.AddSiteSettingsPropertyDeploymentStep<AuditTrailUserEventSettings>();
}
