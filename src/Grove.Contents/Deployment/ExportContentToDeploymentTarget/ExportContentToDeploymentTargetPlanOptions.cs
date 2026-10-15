using Grove.Deployment;
using Grove.Settings;

namespace Grove.Contents.Deployment.ExportContentToDeploymentTarget;

/// <summary>The deployment plans with an "Export content to deployment target" step.</summary>
public sealed class ExportContentToDeploymentTargetPlanOptions(IDeploymentPlanService plans) : ISettingOptions
{
    public async Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default) =>
        [.. (await plans.GetAllDeploymentPlansAsync())
            .Where(plan => plan.DeploymentSteps.Any(step => step.Name == nameof(ExportContentToDeploymentTargetDeploymentStep)))
            .Select(plan => new SettingOption(plan.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), plan.Name))];
}
