using Fluid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Grove.FileStorage.AzureBlob;

namespace Grove.Media.Azure.Services;

internal sealed class MediaBlobFilesStorageOptionsConfiguration : BlobStorageOptionsConfiguration<MediaBlobFilesStorageOptions>
{
    public const string Section = "Grove_Media_Azure_Files";

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
