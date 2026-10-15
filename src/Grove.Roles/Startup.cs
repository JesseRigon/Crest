using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Grove.Localization.Data;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Roles.Deployment;
using Grove.Roles.Migrations;
using Grove.Roles.Recipes;
using Grove.Roles.Services;
using Grove.Security;
using Grove.Security.Permissions;
using Grove.Security.Services;
using Grove.Users.Services;

namespace Grove.Roles;

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
        services.AddScoped<IPermissionSetService, PermissionSetService>();
        services.Replace(ServiceDescriptor.Scoped<IRoleClaimStore<IRole>>(sp => sp.GetRequiredService<RoleStore>()));
        services.Replace(ServiceDescriptor.Scoped<IRoleStore<IRole>>(sp => sp.GetRequiredService<RoleStore>()));
        services.AddRecipeExecutionStep<RolesStep>();
        services.AddPermissionProvider<Permissions>();
        services.AddApiContributor<Api.RolesApi>();
        services.Configure<SystemRoleOptions>(options =>
        {
            var adminRoleName = _shellConfiguration.GetSection("Grove_Roles").GetValue<string>("AdminRoleName");

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

[RequireFeatures("Grove.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AllRolesDeploymentSource, AllRolesDeploymentStep, AllRolesDeploymentStepDriver>();
    }
}

[Feature("Grove.Roles.Core")]
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

[RequireFeatures("Grove.DataLocalization")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, PermissionsLocalizationDataProvider>();
    }
}
