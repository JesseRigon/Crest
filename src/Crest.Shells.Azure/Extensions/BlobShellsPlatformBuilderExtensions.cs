using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Configuration;
using Crest.FileStorage.AzureBlob;
using Crest.Modules;
using Crest.Shells.Azure.Configuration;
using Crest.Shells.Azure.Services;

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

            var blobOptions = configuration.GetSectionCompat("Crest:Crest_Shells_Azure")
                .Get<BlobShellStorageOptions>()
                ?? throw new Exception("The 'Crest.Shells.Azure' configuration section must be defined");

            var clock = sp.GetRequiredService<IClock>();
            var contentTypeProvider = sp.GetRequiredService<IContentTypeProvider>();

            var fileStore = new BlobFileStore(blobOptions, clock, contentTypeProvider);

            return new BlobShellsFileStore(fileStore);
        });

        services.Replace(ServiceDescriptor.Singleton<IShellsSettingsSources>(sp =>
        {
            var shellsFileStore = sp.GetRequiredService<IShellsFileStore>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var blobOptions = configuration.GetSectionCompat("Crest:Crest_Shells_Azure").Get<BlobShellStorageOptions>();
            var shellOptions = sp.GetRequiredService<IOptions<ShellOptions>>();

            return new BlobShellsSettingsSources(shellsFileStore, blobOptions, shellOptions);
        }));

        services.Replace(ServiceDescriptor.Singleton<IShellConfigurationSources>(sp =>
        {
            var shellsFileStore = sp.GetRequiredService<IShellsFileStore>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var blobOptions = configuration.GetSectionCompat("Crest:Crest_Shells_Azure").Get<BlobShellStorageOptions>();
            var shellOptions = sp.GetRequiredService<IOptions<ShellOptions>>();

            return new BlobShellConfigurationSources(shellsFileStore, blobOptions, shellOptions);
        }));

        services.Replace(ServiceDescriptor.Singleton<IShellsConfigurationSources>(sp =>
        {
            var shellsFileStore = sp.GetRequiredService<IShellsFileStore>();
            var environment = sp.GetRequiredService<IHostEnvironment>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var blobOptions = configuration.GetSectionCompat("Crest:Crest_Shells_Azure").Get<BlobShellStorageOptions>();
            var shellOptions = sp.GetRequiredService<IOptions<ShellOptions>>();

            return new BlobShellsConfigurationSources(shellsFileStore, environment, blobOptions, shellOptions);
        }));

        return builder;
    }
}
