using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.Contents.ViewModels;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.Modules;
using Crest.Navigation;
using Crest.Settings.Deployment;

namespace Crest.Contents.Deployment.ExportContentToDeploymentTarget;

[Feature("Crest.Contents.Deployment.ExportContentToDeploymentTarget")]
public sealed class ExportContentToDeploymentTargetStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<ExportContentToDeploymentTargetAdminMenu>();

        services.AddSiteDisplayDriver<ExportContentToDeploymentTargetSettingsDisplayDriver>();

        services.AddDeployment<ExportContentToDeploymentTargetDeploymentSource, ExportContentToDeploymentTargetDeploymentStep, ExportContentToDeploymentTargetDeploymentStepDriver>();

        services.AddDataMigration<ExportContentToDeploymentTargetMigrations>();
        services.AddScoped<IContentDisplayDriver, ExportContentToDeploymentTargetContentDriver>();
        services.AddDisplayDriver<ContentOptionsViewModel, ExportContentToDeploymentTargetContentsAdminListDisplayDriver>();

        services.AddSiteSettingsPropertyDeploymentStep<ExportContentToDeploymentTargetSettings, ExportContentToDeploymentTargetStartup>(S => S["Export Content To Deployment Target settings"], S => S["Exports the Export Content To Deployment Target settings."]);
    }
}
