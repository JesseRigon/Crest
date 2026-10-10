using Fluid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Configuration;
using Crest.FileStorage.AzureBlob;

namespace Crest.Media.Azure.Services;

internal sealed class MediaBlobImageCacheOptionsConfiguration : BlobStorageOptionsConfiguration<MediaBlobImageCacheOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    public MediaBlobImageCacheOptionsConfiguration(
        FluidParser fluidParser,
        IShellConfiguration shellConfiguration,
        ShellSettings shellSettings,
        ILogger<MediaBlobImageCacheOptionsConfiguration> logger)
         : base(fluidParser, shellSettings, logger)
    {
        _shellConfiguration = shellConfiguration;
    }

    protected override void FurtherConfigure(MediaBlobImageCacheOptions rawOptions, MediaBlobImageCacheOptions options)
    {
        options.CreateContainer = rawOptions.CreateContainer;
        options.RemoveContainer = rawOptions.RemoveContainer;
    }

    protected override MediaBlobImageCacheOptions GetRawOptions()
        => _shellConfiguration.GetSection("Crest_Media_Azure_Image_Cache")
        .Get<MediaBlobImageCacheOptions>();
}
