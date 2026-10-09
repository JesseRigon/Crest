using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Configuration;
using Crest.Shells.Azure.Services;

namespace Crest.Shells.Azure.Configuration;

public class BlobShellsConfigurationSources : IShellsConfigurationSources
{
    private static readonly string s_appSettings =
        Path.GetFileNameWithoutExtension(PlatformConstants.Configuration.ApplicationSettingsFileName);

    private readonly IShellsFileStore _shellsFileStore;
    private readonly BlobShellStorageOptions _blobOptions;

    private readonly string _environment;
    private readonly string _fileSystemAppSettings;

    public BlobShellsConfigurationSources(
        IShellsFileStore shellsFileStore,
        IHostEnvironment hostingEnvironment,
        BlobShellStorageOptions blobOptions,
        IOptions<ShellOptions> shellOptions
        )
    {
        _shellsFileStore = shellsFileStore;
        _environment = hostingEnvironment.EnvironmentName;
        _blobOptions = blobOptions;
        _fileSystemAppSettings = Path.Combine(shellOptions.Value.ShellsApplicationDataPath, s_appSettings);
    }

    public async Task AddSourcesAsync(IConfigurationBuilder builder)
    {
        var appSettingsFileInfo = await _shellsFileStore.GetFileInfoAsync(PlatformConstants.Configuration.ApplicationSettingsFileName);

        if (appSettingsFileInfo == null && _blobOptions.MigrateFromFiles)
        {
            if (await TryMigrateFromFileAsync($"{_fileSystemAppSettings}.json", PlatformConstants.Configuration.ApplicationSettingsFileName))
            {
                appSettingsFileInfo = await _shellsFileStore.GetFileInfoAsync(PlatformConstants.Configuration.ApplicationSettingsFileName);
            }
        }

        if (appSettingsFileInfo != null)
        {
            var stream = await _shellsFileStore.GetFileStreamAsync(PlatformConstants.Configuration.ApplicationSettingsFileName);
            builder.AddTenantJsonStream(stream);
        }

        var environmentAppSettingsFileName = $"{s_appSettings}.{_environment}.json";
        var environmentAppSettingsFileInfo = await _shellsFileStore.GetFileInfoAsync(environmentAppSettingsFileName);
        if (environmentAppSettingsFileInfo == null && _blobOptions.MigrateFromFiles)
        {
            if (await TryMigrateFromFileAsync($"{_fileSystemAppSettings}.{_environment}.json", environmentAppSettingsFileName))
            {
                environmentAppSettingsFileInfo = await _shellsFileStore.GetFileInfoAsync(environmentAppSettingsFileName);
            }
            else
            {
                return;
            }
        }

        if (environmentAppSettingsFileInfo != null)
        {
            var stream = await _shellsFileStore.GetFileStreamAsync(environmentAppSettingsFileName);
            builder.AddTenantJsonStream(stream);
        }
    }

    private async Task<bool> TryMigrateFromFileAsync(string fileSystemPath, string destPath)
    {
        if (!File.Exists(fileSystemPath))
        {
            return false;
        }

        using var file = File.OpenRead(fileSystemPath);
        await _shellsFileStore.CreateFileFromStreamAsync(destPath, file);

        return true;
    }
}
