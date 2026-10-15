using Grove.Workflows.Common.Codecs;
using Grove.Workflows.Common.Services;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Services;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Common.Features;

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