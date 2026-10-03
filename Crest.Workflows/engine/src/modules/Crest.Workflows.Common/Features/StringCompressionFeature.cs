using Crest.Workflows.Common.Codecs;
using Crest.Workflows.Common.Services;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Services;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Common.Features;

[UsedImplicitly]
public class StringCompressionFeature(IModule module) : FeatureBase(module)
{
    public override void Apply()
    {
        Services
            .AddSingleton<ICompressionCodecResolver, CompressionCodecResolver>()
            .AddSingleton<ICompressionCodec, None>()
            .AddSingleton<ICompressionCodec, GZip>()
            .AddSingleton<ICompressionCodec, Zstd>();
    }
}