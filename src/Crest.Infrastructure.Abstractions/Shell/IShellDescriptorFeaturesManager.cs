using Crest.Environment.Extensions.Features;
using Crest.Environment.Shell.Descriptor.Models;

namespace Crest.Environment.Shell;

public delegate void FeatureDependencyNotificationHandler(string messageFormat, IFeatureInfo feature, IEnumerable<IFeatureInfo> features);

public interface IShellDescriptorFeaturesManager
{
    Task<(IEnumerable<IFeatureInfo>, IEnumerable<IFeatureInfo>)> UpdateFeaturesAsync(ShellDescriptor shellDescriptor,
        IEnumerable<IFeatureInfo> featuresToDisable, IEnumerable<IFeatureInfo> featuresToEnable, bool force);
}
