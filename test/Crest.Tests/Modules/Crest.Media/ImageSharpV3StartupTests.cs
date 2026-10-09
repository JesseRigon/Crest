#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Crest.Media.ImageSharpV3.Engine;
using Crest.Media.Processing;

namespace Crest.Tests.Modules.Crest.Media;

public sealed class ImageSharpV3StartupTests
{
    [Fact]
    public void ConfigureServices_ReplacesDefaultEngineWithImageSharp_Succeeds()
    {
        var services = new ServiceCollection();

        // Mirror the default registration from the Crest.Media module.
        services.AddSingleton<IImageProcessingEngine, VipsImageProcessingEngine>();

        // The ImageSharpV3 feature Startup runs after the Media module and replaces the engine.
        new global::Crest.Media.ImageSharpV3.Startup().ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        var engine = provider.GetRequiredService<IImageProcessingEngine>();

        Assert.IsType<ImageSharpImageProcessingEngine>(engine);

        // A single engine must be registered after the replacement.
        Assert.Single(services, d => d.ServiceType == typeof(IImageProcessingEngine));
    }
}
