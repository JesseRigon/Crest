using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Media.ImageSharpV3.Engine;
using Grove.Media.Processing;
using Grove.Modules;

namespace Grove.Media.ImageSharpV3;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IImageProcessingEngine, ImageSharpImageProcessingEngine>());
    }
}
