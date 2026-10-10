using Crest.Environment.Extensions.Features;
using Crest.Environment.Extensions.Manifests;
using Crest.Modules;

namespace Crest.Environment.Extensions;

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
