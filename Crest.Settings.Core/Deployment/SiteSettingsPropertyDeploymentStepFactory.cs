using Crest.Deployment;

namespace Crest.Settings.Deployment;

public class SiteSettingsPropertyDeploymentStepFactory<TModel> : IDeploymentStepFactory
    where TModel : class, new()
{
    private static readonly string s_genericTypeKey = typeof(TModel).Name + "_SiteSettingsPropertyDeploymentStep";

    public string Name => s_genericTypeKey;

    public DeploymentStep Create()
    {
        return new SiteSettingsPropertyDeploymentStep<TModel>();
    }
}
