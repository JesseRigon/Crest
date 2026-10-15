using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Entities;
using Grove.Infrastructure.Recipes;
using Grove.Infrastructure.Recipes.Services;
using Grove.Infrastructure.Settings;

namespace Grove.Contents.Deployment.ExportContentToDeploymentTarget;

public sealed class ExportContentToDeploymentTargetMigrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;
    private readonly IDeploymentPlanService _deploymentPlanService;
    private readonly ISiteService _siteService;

    public ExportContentToDeploymentTargetMigrations(
        IRecipeMigrator recipeMigrator,
        IDeploymentPlanService deploymentPlanService,
        ISiteService siteService
        )
    {
        _recipeMigrator = recipeMigrator;
        _deploymentPlanService = deploymentPlanService;
        _siteService = siteService;
    }

    public async Task<int> CreateAsync()
    {
        await _recipeMigrator.ExecuteAsync($"exportcontenttodeploymenttarget{RecipesConstants.RecipeExtension}", this);

        var deploymentPlans = await _deploymentPlanService.GetAllDeploymentPlansAsync();
        var exportContentToDeploymentTargetPlan = deploymentPlans.FirstOrDefault(x => x.DeploymentSteps.Any(x => x.Name == nameof(ExportContentToDeploymentTargetDeploymentStep)));

        if (exportContentToDeploymentTargetPlan != null)
        {
            var siteSettings = await _siteService.LoadSiteSettingsAsync();
            siteSettings.Alter<ExportContentToDeploymentTargetSettings>(aspect => aspect.ExportContentToDeploymentTargetPlanId = exportContentToDeploymentTargetPlan.Id);

            await _siteService.UpdateSiteSettingsAsync(siteSettings);
        }

        return 1;
    }
}
