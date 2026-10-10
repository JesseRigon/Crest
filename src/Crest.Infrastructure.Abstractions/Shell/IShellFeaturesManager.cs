using Crest.Environment.Extensions;
using Crest.Environment.Extensions.Features;

namespace Crest.Environment.Shell;

public interface IShellFeaturesManager
{
    Task<IEnumerable<IFeatureInfo>> GetAvailableFeaturesAsync();
    Task<IEnumerable<IFeatureInfo>> GetEnabledFeaturesAsync();
    Task<IEnumerable<IFeatureInfo>> GetAlwaysEnabledFeaturesAsync();
    Task<IEnumerable<IFeatureInfo>> GetDisabledFeaturesAsync();
    Task<(IEnumerable<IFeatureInfo>, IEnumerable<IFeatureInfo>)> UpdateFeaturesAsync(
        IEnumerable<IFeatureInfo> featuresToDisable, IEnumerable<IFeatureInfo> featuresToEnable, bool force);
    Task<IEnumerable<IExtensionInfo>> GetEnabledExtensionsAsync();
}
