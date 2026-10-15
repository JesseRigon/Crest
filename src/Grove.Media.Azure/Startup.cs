using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Data.Migration;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Grove.Environment.Shell.Scope;
using Grove.FileStorage;
using Grove.FileStorage.AzureBlob;
using Grove.Media.Azure.Services;
using Grove.Media;
using Grove.Media.Events;
using Grove.Media.Files;
using Grove.Media.Library;
using Grove.Media.Services;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;

namespace Grove.Media.Azure;

[Feature("Grove.Media.Azure.Storage")]
public sealed class Startup : Modules.StartupBase
{
    private readonly ILogger _logger;
    private readonly IShellConfiguration _configuration;

    public Startup(ILogger<Startup> logger, IShellConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddApiContributor<Api.AzureMediaOptionsApi>();
        services.AddTransient<IConfigureOptions<MediaBlobStorageOptions>, MediaBlobStorageOptionsConfiguration>();

        // Only replace default implementation if options are valid.
        var section = _configuration.GetSection("Grove_Media_Azure");
        var connectionString = section.GetValue<string>(nameof(MediaBlobStorageOptions.ConnectionString));
        var containerName = section.GetValue<string>(nameof(MediaBlobStorageOptions.ContainerName));

        if (CheckOptions(connectionString, containerName, _logger))
        {
            // Register a media cache file provider.
            services.AddSingleton<IMediaFileStoreCacheFileProvider>(serviceProvider =>
            {
                var hostingEnvironment = serviceProvider.GetRequiredService<IWebHostEnvironment>();

                if (string.IsNullOrWhiteSpace(hostingEnvironment.WebRootPath))
                {
                    throw new MediaConfigurationException("The wwwroot folder for serving cache media files is missing.");
                }

                var mediaOptions = serviceProvider.GetRequiredService<IOptions<MediaOptions>>().Value;
                var shellOptions = serviceProvider.GetRequiredService<IOptions<ShellOptions>>();
                var shellSettings = serviceProvider.GetRequiredService<ShellSettings>();
                var logger = serviceProvider.GetRequiredService<ILogger<DefaultMediaFileStoreCacheFileProvider>>();

                var mediaCachePath = GetMediaCachePath(
                    hostingEnvironment, shellSettings, DefaultMediaFileStoreCacheFileProvider.AssetsCachePath);

                if (!Directory.Exists(mediaCachePath))
                {
                    Directory.CreateDirectory(mediaCachePath);
                }

                return new DefaultMediaFileStoreCacheFileProvider(logger, mediaOptions.AssetsRequestPath, mediaCachePath);
            });

            // Replace the default media file provider with the media cache file provider.
            services.Replace(ServiceDescriptor.Singleton<IMediaFileProvider>(serviceProvider =>
                serviceProvider.GetRequiredService<IMediaFileStoreCacheFileProvider>()));

            // Register the media cache file provider as a file store cache provider.
            services.AddSingleton<IMediaFileStoreCache>(serviceProvider =>
                serviceProvider.GetRequiredService<IMediaFileStoreCacheFileProvider>());

            // Register the blob file store as a singleton so it can be injected for async initialization.
            services.AddSingleton(serviceProvider =>
            {
                var blobStorageOptions = serviceProvider.GetRequiredService<IOptions<MediaBlobStorageOptions>>().Value;
                var clock = serviceProvider.GetRequiredService<IClock>();
                var contentTypeProvider = serviceProvider.GetRequiredService<IContentTypeProvider>();
                var blobLogger = serviceProvider.GetRequiredService<ILogger<BlobFileStore>>();

                return new BlobFileStore(blobStorageOptions, clock, contentTypeProvider, blobLogger);
            });

            // The media library's paths (ruling F1) stay the library drive; before a tenant's
            // import has run they are this container, and the import reads it (docs/media.md ›
            // The media library). IMediaFileStore itself is never replaced.
            services.Replace(ServiceDescriptor.Singleton(serviceProvider =>
                new LibraryFileStore(serviceProvider.GetRequiredService<BlobFileStore>())));

            // File objects' bytes in Blob Storage (requirement 11), under their own base path.
            services.AddTransient<IConfigureOptions<MediaBlobFilesStorageOptions>, MediaBlobFilesStorageOptionsConfiguration>();
            services.Replace(ServiceDescriptor.Singleton<IMediaStorageProvider>(AzureBlobMediaStorage.Create));

            services.AddSingleton<IMediaEventHandler, DefaultMediaFileStoreCacheEventHandler>();

            services.AddScoped<IModularTenantEvents, MediaBlobContainerTenantEvents>();
        }
    }

    private static string GetMediaCachePath(IWebHostEnvironment hostingEnvironment, ShellSettings shellSettings, string assetsPath)
        => PathExtensions.Combine(hostingEnvironment.WebRootPath, shellSettings.Name, assetsPath);

    // Enabled without configuration is an expected state (a tenant enables every feature in
    // dev): one warning naming what is missing, and the local media store stays in place.
    private static bool CheckOptions(string connectionString, string containerName, ILogger logger)
        => AzureMediaConfiguration.Check("Azure Media Storage", "Grove_Media_Azure", connectionString, containerName, logger);
}

internal static class AzureMediaConfiguration
{
    /// <summary>Whether the media container is configured (the Tus temp store needs it too).</summary>
    public static bool IsConfigured(IShellConfiguration configuration)
    {
        var section = configuration.GetSection("Grove_Media_Azure");
        return !string.IsNullOrWhiteSpace(section.GetValue<string>(nameof(MediaBlobStorageOptions.ConnectionString)))
            && !string.IsNullOrWhiteSpace(section.GetValue<string>(nameof(MediaBlobStorageOptions.ContainerName)));
    }

    public static bool Check(string name, string section, string connectionString, string containerName, ILogger logger)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            missing.Add(nameof(MediaBlobStorageOptions.ConnectionString));
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            missing.Add(nameof(MediaBlobStorageOptions.ContainerName));
        }

        if (missing.Count > 0)
        {
            logger.LogWarning("{Name} is enabled but not active because '{Section}' is missing {Missing} in application configuration.", name, section, string.Join(" and ", missing));
            return false;
        }

        return true;
    }
}

[Feature("Grove.Media.Azure.ImageCache")]
public sealed class MediaAzureImageCacheStartup : Modules.StartupBase
{
    private readonly IShellConfiguration _configuration;
    private readonly ILogger _logger;

    public MediaAzureImageCacheStartup(
        IShellConfiguration configuration,
        ILogger<MediaAzureImageCacheStartup> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<MediaBlobImageCacheOptions>, MediaBlobImageCacheOptionsConfiguration>();

        // Only replace the default local cache implementation if options are valid.
        var section = _configuration.GetSection("Grove_Media_Azure_Image_Cache");
        var connectionString = section.GetValue<string>(nameof(MediaBlobStorageOptions.ConnectionString));
        var containerName = section.GetValue<string>(nameof(MediaBlobStorageOptions.ContainerName));

        if (!CheckOptions(connectionString, containerName))
        {
            return;
        }

        services.Replace(ServiceDescriptor.Singleton<IResizedImageCache, AzureBlobResizedImageCache>());

        services.AddScoped<IModularTenantEvents, MediaBlobImageCacheTenantEvents>();
    }

    private bool CheckOptions(string connectionString, string containerName)
        => AzureMediaConfiguration.Check("Azure Media ImageSharp Image Cache", "Grove_Media_Azure_Image_Cache", connectionString, containerName, _logger);
}

[Feature("Grove.Media.Azure.Storage")]
[RequireFeatures("Grove.Media.Tus")]
public sealed class MediaAzureTusStartup(IShellConfiguration configuration) : Modules.StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Upload chunks go to the media container only when it is configured; otherwise the
        // local temp store stays (the storage startup has already warned).
        if (AzureMediaConfiguration.IsConfigured(configuration))
        {
            services.Replace(ServiceDescriptor.Singleton<ITusTempStore, AzureBlobTusTempStore>());
        }
    }
}

// Keeps the renamed feature enabled on sites that had the legacy
// "Grove.Media.Azure.ImageSharpImageCache" feature enabled before the rename. The legacy
// feature depends on the new one, and this migration explicitly enables the new feature so it
// remains active in its own right once the obsolete feature is removed.
[Feature("Grove.Media.Azure.ImageSharpImageCache")]
public sealed class LegacyImageCacheFeatureStartup : Modules.StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<LegacyImageCacheFeatureMigrations>();
    }
}

internal sealed class LegacyImageCacheFeatureMigrations : DataMigration
{
    public static int Create()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var featuresManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();

            if (await featuresManager.IsFeatureEnabledAsync("Grove.Media.Azure.ImageCache"))
            {
                return;
            }

            await featuresManager.EnableFeaturesAsync("Grove.Media.Azure.ImageCache");
        });

        return 1;
    }
}
