using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Display.Management.Handlers;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Settings.Deployment;

namespace Grove.Contents.Deployment.ExportContentToDeploymentTarget;

[Feature("Grove.Contents.Deployment.ExportContentToDeploymentTarget")]
public sealed class ExportContentToDeploymentTargetStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSettingsGroup<ExportContentToDeploymentTargetSettings>(
            ExportContentToDeploymentTargetSettings.GroupId,
            "Deployment Targets",
            DeploymentPermissions.ManageDeploymentPlan,
            group => group.InMenu("settings", entryId: "deployment-targets"));


        services.AddDeployment<ExportContentToDeploymentTargetDeploymentSource, ExportContentToDeploymentTargetDeploymentStep>();

        services.AddDataMigration<ExportContentToDeploymentTargetMigrations>();

        services.AddSiteSettingsPropertyDeploymentStep<ExportContentToDeploymentTargetSettings>();
    }
}
