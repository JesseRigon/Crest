using Grove.Environment.Extensions.Features;
using Grove.Environment.Extensions.Manifests;
using Grove.Modules;

namespace Grove.Environment.Extensions;

public class NotFoundExtensionInfo : IExtensionInfo
{
    public NotFoundExtensionInfo(string extensionId)
    {
        Id = extensionId;
        SubPath = Application.ModulesRoot + extensionId;
        Manifest = new NotFoundManifestInfo();
        Features = [];
    }

    public string Id { get; }
    public string SubPath { get; }
    public IManifestInfo Manifest { get; }
    public IEnumerable<IFeatureInfo> Features { get; }
    public bool Exists => false;
}
