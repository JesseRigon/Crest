using System.Text;
using Amazon;
using Amazon.S3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Data.Migration;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Grove.Environment.Shell.Scope;
using Grove.FileStorage;
using Grove.FileStorage.AmazonS3;
using Grove.Media.AmazonS3.Services;
using Grove.Media;
using Grove.Media.Events;
using Grove.Media.Files;
using Grove.Media.Library;
using Grove.Media.Services;
using Grove.Modules;
using Grove.Navigation;
using Grove.Security.Permissions;

namespace Grove.Media.AmazonS3;

public sealed class Startup : Modules.StartupBase
{
    private readonly ILogger _logger;
    private readonly IShellConfiguration _configuration;

    public Startup(IShellConfiguration configuration,
        ILogger<Startup> logger)
        => (_configuration, _logger)
            = (configuration, logger);

    static Startup() => AWSConfigs.InitializeCollections = true;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddPageSource<AdminPages>();
        services.AddTransient<IConfigureOptions<AwsStorageOptions>, AwsStorageOptionsConfiguration>();

        var storeOptions = new AwsStorageOptions().BindConfiguration(AmazonS3Constants.ConfigSections.AmazonS3, _configuration, _logger);
        var validationErrors = storeOptions.Validate().ToList();
        var stringBuilder = new StringBuilder();

        if (validationErrors.Count > 0)
        {
            foreach (var error in validationErrors)
            {
                stringBuilder.Append(error.ErrorMessage);
            }

            _logger.LogError("S3 Media configuration validation failed with errors: {Errors} fallback to local file storage.", stringBuilder);
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Starting with S3 Media Configuration. BucketName: {BucketName}; BasePath: {BasePath}", storeOptions.BucketName, storeOptions.BasePath);
            }

            services.AddSingleton<IMediaFileStoreCacheFileProvider>(serviceProvider =>
            {
                var hostingEnvironment = serviceProvider.GetRequiredService<IWebHostEnvironment>();

                if (string.IsNullOrWhiteSpace(hostingEnvironment.WebRootPath))
                {
                    throw new MediaConfigurationException("The wwwroot folder for serving cache media files is missing.");
                }

                var mediaOptions = serviceProvider.GetRequiredService<IOptions<MediaOptions>>().Value;
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

            // Registering IAmazonS3 client using AWS registration factory.
            services.AddAWSService<IAmazonS3>(storeOptions.AwsOptions);

            // The media library's paths (ruling F1) stay the library drive; before a tenant's
            // import has run they are this bucket, and the import reads it (docs/media.md › The
            // media library). IMediaFileStore itself is never replaced.
            services.Replace(ServiceDescriptor.Singleton(serviceProvider => new LibraryFileStore(new AwsFileStore(
                serviceProvider.GetRequiredService<IClock>(),
                serviceProvider.GetRequiredService<IOptions<AwsStorageOptions>>().Value,
                serviceProvider.GetRequiredService<IAmazonS3>()))));

            // File objects' bytes in S3 (requirement 11), under their own base path.
            services.Replace(ServiceDescriptor.Singleton<IMediaStorageProvider>(S3MediaStorage.Create));

            services.AddSingleton<IMediaEventHandler, DefaultMediaFileStoreCacheEventHandler>();

            services.AddScoped<IModularTenantEvents, MediaS3BucketTenantEvents>();
        }
    }

    private static string GetMediaCachePath(IWebHostEnvironment hostingEnvironment, ShellSettings shellSettings, string assetsPath)
        => PathExtensions.Combine(hostingEnvironment.WebRootPath, shellSettings.Name, assetsPath);
}

[Feature("Grove.Media.AmazonS3.ImageCache")]
public sealed class MediaAmazonS3ImageCacheStartup : Modules.StartupBase
{
    private readonly IShellConfiguration _configuration;
    private readonly ILogger _logger;

    public MediaAmazonS3ImageCacheStartup(
        IShellConfiguration configuration,
        ILogger<MediaAmazonS3ImageCacheStartup> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AwsMediaImageCacheOptions>, AwsMediaImageCacheOptionsConfiguration>();

        var storeOptions = new AwsStorageOptions().BindConfiguration(AmazonS3Constants.ConfigSections.AmazonS3ImageCache, _configuration, _logger);
        var validationErrors = storeOptions.Validate().ToList();
        var stringBuilder = new StringBuilder();

        if (validationErrors.Count > 0)
        {
            foreach (var error in validationErrors)
            {
                stringBuilder.Append(error.ErrorMessage);
            }

            _logger.LogError("S3 Media Image Cache configuration validation failed with errors: {Errors} — falling back to local file storage.", stringBuilder);
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Starting with S3 Media Image Cache configuration. BucketName: {BucketName}; BasePath: {BasePath}", storeOptions.BucketName, storeOptions.BasePath);
            }

            services.Replace(ServiceDescriptor.Singleton<IResizedImageCache, AWSS3ResizedImageCache>());

            services.AddScoped<IModularTenantEvents, AwsS3MediaImageCacheTenantEvents>();
        }
    }
}

[RequireFeatures("Grove.Media.Tus")]
public sealed class MediaAmazonS3TusStartup : Modules.StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ITusTempStore, S3TusTempStore>());
    }
}

// Keeps the renamed feature enabled on sites that had the legacy
// "Grove.Media.AmazonS3.ImageSharpImageCache" feature enabled before the rename. The legacy
// feature depends on the new one, and this migration explicitly enables the new feature so it
// remains active in its own right once the obsolete feature is removed.
[Feature("Grove.Media.AmazonS3.ImageSharpImageCache")]
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

            if (await featuresManager.IsFeatureEnabledAsync("Grove.Media.AmazonS3.ImageCache"))
            {
                return;
            }

            await featuresManager.EnableFeaturesAsync("Grove.Media.AmazonS3.ImageCache");
        });

        return 1;
    }
}
