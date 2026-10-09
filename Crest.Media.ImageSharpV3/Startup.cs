using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.Media.ImageSharpV3.Engine;
using Crest.Media.Processing;
using Crest.Modules;

namespace Crest.Media.ImageSharpV3;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IImageProcessingEngine, ImageSharpImageProcessingEngine>());
    }
}
