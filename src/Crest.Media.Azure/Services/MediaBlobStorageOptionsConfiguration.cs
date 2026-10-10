using Fluid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Configuration;
using Crest.FileStorage.AzureBlob;

namespace Crest.Media.Azure.Services;

internal sealed class MediaBlobStorageOptionsConfiguration : BlobStorageOptionsConfiguration<MediaBlobStorageOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    public MediaBlobStorageOptionsConfiguration(
        FluidParser fluidParser,
        IShellConfiguration shellConfiguration,
        ShellSettings shellSettings,
        ILogger<MediaBlobStorageOptionsConfiguration> logger)
        : base(fluidParser, shellSettings, logger)
    {
        _shellConfiguration = shellConfiguration;
    }

    protected override MediaBlobStorageOptions GetRawOptions()
        => _shellConfiguration.GetSection("Crest_Media_Azure")
        .Get<MediaBlobStorageOptions>();

    protected override void FurtherConfigure(MediaBlobStorageOptions rawOptions, MediaBlobStorageOptions options)
    {
        options.CreateContainer = rawOptions.CreateContainer;
        options.RemoveContainer = rawOptions.RemoveContainer;
        options.RemoveFilesFromBasePath = rawOptions.RemoveFilesFromBasePath;
        options.UseHierarchicalNamespace = rawOptions.UseHierarchicalNamespace;
    }
}
