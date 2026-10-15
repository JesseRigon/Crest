using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.Contents.ViewModels;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.DisplayManagement.Handlers;
using Grove.Modules;
using Grove.Navigation;
using Grove.Settings.Deployment;

namespace Grove.Contents.Deployment.ExportContentToDeploymentTarget;

[Feature("Grove.Contents.Deployment.ExportContentToDeploymentTarget")]
public sealed class ExportContentToDeploymentTargetStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPageSource<ExportContentToDeploymentTargetPages>();

        services.AddSiteDisplayDriver<ExportContentToDeploymentTargetSettingsDisplayDriver>();

        services.AddDeployment<ExportContentToDeploymentTargetDeploymentSource, ExportContentToDeploymentTargetDeploymentStep, ExportContentToDeploymentTargetDeploymentStepDriver>();

        services.AddDataMigration<ExportContentToDeploymentTargetMigrations>();
        services.AddScoped<IContentDisplayDriver, ExportContentToDeploymentTargetContentDriver>();
        services.AddDisplayDriver<ContentOptionsViewModel, ExportContentToDeploymentTargetContentsAdminListDisplayDriver>();

        services.AddSiteSettingsPropertyDeploymentStep<ExportContentToDeploymentTargetSettings, ExportContentToDeploymentTargetStartup>(S => S["Export Content To Deployment Target settings"], S => S["Exports the Export Content To Deployment Target settings."]);
    }
}
