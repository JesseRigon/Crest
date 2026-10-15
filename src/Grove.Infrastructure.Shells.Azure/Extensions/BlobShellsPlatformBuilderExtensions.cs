using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Grove.Data.Storage.AzureBlob;
using Grove.Modules;
using Grove.Infrastructure.Shells.Azure.Configuration;
using Grove.Infrastructure.Shells.Azure.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class BlobShellsPlatformBuilderExtensions
{
    /// <summary>
    /// Host services to load site and shell settings from Azure Blob Storage.
    /// </summary>
    public static PlatformBuilder AddAzureShellsConfiguration(this PlatformBuilder builder)
    {
        var services = builder.ApplicationServices;

        services.TryAddSingleton<IContentTypeProvider, FileExtensionContentTypeProvider>();

        services.AddSingleton<IShellsFileStore>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();

            var blobOptions = configuration.GetSectionCompat("Grove:Grove_Shells_Azure")
                .Get<BlobShellStorageOptions>()
                ?? throw new Exception("The 'Grove.Infrastructure.Shells.Azure' configuration section must be defined");

            var clock = sp.GetRequiredService<IClock>();
            var contentTypeProvider = sp.GetRequiredService<IContentTypeProvider>();

            var fileStore = new BlobFileStore(blobOptions, clock, contentTypeProvider);

            return new BlobShellsFileStore(fileStore);
        });

        services.Replace(ServiceDescriptor.Singleton<IShellsSettingsSources>(sp =>
        {
            var shellsFileStore = sp.GetRequiredService<IShellsFileStore>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var blobOptions = configuration.GetSectionCompat("Grove:Grove_Shells_Azure").Get<BlobShellStorageOptions>();
            var shellOptions = sp.GetRequiredService<IOptions<ShellOptions>>();

            return new BlobShellsSettingsSources(shellsFileStore, blobOptions, shellOptions);
        }));

        services.Replace(ServiceDescriptor.Singleton<IShellConfigurationSources>(sp =>
        {
            var shellsFileStore = sp.GetRequiredService<IShellsFileStore>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var blobOptions = configuration.GetSectionCompat("Grove:Grove_Shells_Azure").Get<BlobShellStorageOptions>();
            var shellOptions = sp.GetRequiredService<IOptions<ShellOptions>>();

            return new BlobShellConfigurationSources(shellsFileStore, blobOptions, shellOptions);
        }));

        services.Replace(ServiceDescriptor.Singleton<IShellsConfigurationSources>(sp =>
        {
            var shellsFileStore = sp.GetRequiredService<IShellsFileStore>();
            var environment = sp.GetRequiredService<IHostEnvironment>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var blobOptions = configuration.GetSectionCompat("Grove:Grove_Shells_Azure").Get<BlobShellStorageOptions>();
            var shellOptions = sp.GetRequiredService<IOptions<ShellOptions>>();

            return new BlobShellsConfigurationSources(shellsFileStore, environment, blobOptions, shellOptions);
        }));

        return builder;
    }
}
