using Crest.Environment.Extensions.Features;
using Crest.Environment.Extensions.Manifests;
using Crest.Modules;

namespace Crest.Environment.Extensions;

public class ExtensionInfo : IExtensionInfo
{
    public ExtensionInfo(string extensionId)
    {
        Id = extensionId;
        SubPath = Application.ModulesRoot + extensionId;
        Manifest = new NotFoundManifestInfo();
        Features = [];
    }

    public ExtensionInfo(
        string subPath,
        IManifestInfo manifestInfo,
        Func<IManifestInfo, IExtensionInfo, IEnumerable<IFeatureInfo>> features)
    {
        Id = manifestInfo.ModuleInfo.Id;
        SubPath = subPath;
        Manifest = manifestInfo;
        Features = features(manifestInfo, this);
    }

    public string Id { get; }
    public string SubPath { get; }
    public IManifestInfo Manifest { get; }
    public IEnumerable<IFeatureInfo> Features { get; }
    public bool Exists => Manifest.Exists;
}
