using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Configuration;
using Crest.Localization.Data;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Roles.Deployment;
using Crest.Roles.Migrations;
using Crest.Roles.Recipes;
using Crest.Roles.Services;
using Crest.Security;
using Crest.Security.Permissions;
using Crest.Security.Services;
using Crest.Users.Services;

namespace Crest.Roles;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IUserClaimsProvider, RoleClaimsProvider>();

        services.AddDataMigration<SystemRolesMigrations>();
        services.AddDataMigration<RolesMigrations>();

        services.AddScoped<RoleStore>();
        services.Replace(ServiceDescriptor.Scoped<IRoleClaimStore<IRole>>(sp => sp.GetRequiredService<RoleStore>()));
        services.Replace(ServiceDescriptor.Scoped<IRoleStore<IRole>>(sp => sp.GetRequiredService<RoleStore>()));
        services.AddRecipeExecutionStep<RolesStep>();
        services.AddScoped<IAuthorizationHandler, RolesPermissionsHandler>();
        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();
        services.Configure<SystemRoleOptions>(options =>
        {
            var adminRoleName = _shellConfiguration.GetSection("Crest_Roles").GetValue<string>("AdminRoleName");

            if (!string.IsNullOrWhiteSpace(adminRoleName))
            {
                options.SystemAdminRoleName = adminRoleName;
            }
            else
            {
                options.SystemAdminRoleName = PlatformConstants.Roles.Administrator;
            }
        });
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AllRolesDeploymentSource, AllRolesDeploymentStep, AllRolesDeploymentStepDriver>();
    }
}

[Feature("Crest.Roles.Core")]
public sealed class RoleUpdaterStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRolesCoreServices();
        services.AddScoped<RoleManager<IRole>>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<RoleUpdater>();
        services.AddScoped<IFeatureEventHandler>(sp => sp.GetRequiredService<RoleUpdater>());
        services.AddScoped<IRoleCreatedEventHandler>(sp => sp.GetRequiredService<RoleUpdater>());
        services.AddScoped<IRoleRemovedEventHandler>(sp => sp.GetRequiredService<RoleUpdater>());
    }
}

[RequireFeatures("Crest.DataLocalization")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, PermissionsLocalizationDataProvider>();
    }
}
