#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Grove.Media.ImageSharpV3.Engine;
using Grove.Media.Processing;

namespace Grove.Tests.Modules.Grove.Media;

public sealed class ImageSharpV3StartupTests
{
    [Fact]
    public void ConfigureServices_ReplacesDefaultEngineWithImageSharp_Succeeds()
    {
        var services = new ServiceCollection();

        // Mirror the default registration from the Grove.Media module.
        services.AddSingleton<IImageProcessingEngine, VipsImageProcessingEngine>();

        // The ImageSharpV3 feature Startup runs after the Media module and replaces the engine.
        new global::Grove.Media.ImageSharpV3.Startup().ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        var engine = provider.GetRequiredService<IImageProcessingEngine>();

        Assert.IsType<ImageSharpImageProcessingEngine>(engine);

        // A single engine must be registered after the replacement.
        Assert.Single(services, d => d.ServiceType == typeof(IImageProcessingEngine));
    }
}
