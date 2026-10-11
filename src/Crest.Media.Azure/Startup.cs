using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Crest.Data.Migration;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Configuration;
using Crest.Environment.Shell.Scope;
using Crest.FileStorage;
using Crest.FileStorage.AzureBlob;
using Crest.Media.Azure.Filters;
using Crest.Media.Azure.Services;
using Crest.Media;
using Crest.Media.Events;
using Crest.Media.Helpers;
using Crest.Media.Events;
using Crest.Media.Services;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;

namespace Crest.Media.Azure;

[Feature("Crest.Media.Azure.Storage")]
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
        services.AddNavigationProvider<AdminMenu>();
        services.AddTransient<IConfigureOptions<MediaBlobStorageOptions>, MediaBlobStorageOptionsConfiguration>();

        // Only replace default implementation if options are valid.
        var section = _configuration.GetSection("Crest_Media_Azure");
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

            // Replace the default media file store with a blob file store.
            services.Replace(ServiceDescriptor.Singleton<IMediaFileStore>(serviceProvider =>
            {
                var fileStore = serviceProvider.GetRequiredService<BlobFileStore>();
                var shellSettings = serviceProvider.GetRequiredService<ShellSettings>();
                var mediaOptions = serviceProvider.GetRequiredService<IOptions<MediaOptions>>().Value;
                var mediaEventHandlers = serviceProvider.GetServices<IMediaEventHandler>();
                var fileSizeHelper = serviceProvider.GetService<FileSizeHelper>();
                var mediaCreatingEventHandlers = serviceProvider.GetServices<IMediaCreatingEventHandler>();
                var logger = serviceProvider.GetRequiredService<ILogger<DefaultMediaFileStore>>();

                var mediaUrlBase = "/" + fileStore.Combine(shellSettings.RequestUrlPrefix, mediaOptions.AssetsRequestPath);

                var originalPathBase = serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext
                    ?.Features.Get<ShellContextFeature>()
                    ?.OriginalPathBase ?? PathString.Empty;

                if (originalPathBase.HasValue)
                {
                    mediaUrlBase = fileStore.Combine(originalPathBase.Value, mediaUrlBase);
                }

                return new DefaultMediaFileStore(
                    fileStore,
                    mediaUrlBase,
                    mediaOptions.CdnBaseUrl,
                    mediaEventHandlers,
                    mediaCreatingEventHandlers,
                    fileSizeHelper,
                    logger);
            }));

            services.AddSingleton<IMediaEventHandler, DefaultMediaFileStoreCacheEventHandler>();

            services.AddScoped<IModularTenantEvents, MediaBlobContainerTenantEvents>();

            services.Configure<MvcOptions>(options => options.Filters.Add<MediaGalleryCapabilitiesFilter>());
        }
    }

    private static string GetMediaCachePath(IWebHostEnvironment hostingEnvironment, ShellSettings shellSettings, string assetsPath)
        => PathExtensions.Combine(hostingEnvironment.WebRootPath, shellSettings.Name, assetsPath);

    private static bool CheckOptions(string connectionString, string containerName, ILogger logger)
    {
        var optionsAreValid = true;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogError("Azure Media Storage is enabled but not active because the 'ConnectionString' is missing or empty in application configuration.");
            optionsAreValid = false;
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            logger.LogError("Azure Media Storage is enabled but not active because the 'ContainerName' is missing or empty in application configuration.");
            optionsAreValid = false;
        }

        return optionsAreValid;
    }
}

[Feature("Crest.Media.Azure.ImageCache")]
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
        var section = _configuration.GetSection("Crest_Media_Azure_Image_Cache");
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
    {
        var optionsAreValid = true;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError(
                "Azure Media ImageSharp Image Cache is enabled but not active because the 'ConnectionString' is missing or empty in application configuration.");
            optionsAreValid = false;
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            _logger.LogError(
                "Azure Media ImageSharp Image Cache is enabled but not active because the 'ContainerName' is missing or empty in application configuration.");
            optionsAreValid = false;
        }

        return optionsAreValid;
    }
}

[Feature("Crest.Media.Azure.Storage")]
[RequireFeatures("Crest.Media.Tus")]
public sealed class MediaAzureTusStartup : Modules.StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ITusTempStore, AzureBlobTusTempStore>());
    }
}

// Keeps the renamed feature enabled on sites that had the legacy
// "Crest.Media.Azure.ImageSharpImageCache" feature enabled before the rename. The legacy
// feature depends on the new one, and this migration explicitly enables the new feature so it
// remains active in its own right once the obsolete feature is removed.
[Feature("Crest.Media.Azure.ImageSharpImageCache")]
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

            if (await featuresManager.IsFeatureEnabledAsync("Crest.Media.Azure.ImageCache"))
            {
                return;
            }

            await featuresManager.EnableFeaturesAsync("Crest.Media.Azure.ImageCache");
        });

        return 1;
    }
}
