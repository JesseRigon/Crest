using Fluid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Configuration;
using Crest.FileStorage.AzureBlob;

namespace Crest.Media.Azure.Services;

internal sealed class MediaBlobFilesStorageOptionsConfiguration : BlobStorageOptionsConfiguration<MediaBlobFilesStorageOptions>
{
    public const string Section = "Crest_Media_Azure_Files";

    private readonly IShellConfiguration _shellConfiguration;

    public MediaBlobFilesStorageOptionsConfiguration(
        FluidParser fluidParser,
        IShellConfiguration shellConfiguration,
        ShellSettings shellSettings,
        ILogger<MediaBlobFilesStorageOptionsConfiguration> logger)
        : base(fluidParser, shellSettings, logger)
    {
        _shellConfiguration = shellConfiguration;
    }

    protected override MediaBlobFilesStorageOptions GetRawOptions()
        => _shellConfiguration.GetSection(Section).Get<MediaBlobFilesStorageOptions>();

    protected override void FurtherConfigure(MediaBlobFilesStorageOptions rawOptions, MediaBlobFilesStorageOptions options)
        => options.UseHierarchicalNamespace = rawOptions.UseHierarchicalNamespace;
}
