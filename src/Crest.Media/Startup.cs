using Fluid;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Crest.BackgroundTasks;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Handlers;
using Crest.ContentTypes.Editors;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Liquid.Tags;
using Crest.Environment.Shell;
using Crest.FileStorage;
using Crest.FileStorage.FileSystem;
using Crest.Indexing;
using Crest.Liquid;
using Crest.Localization;
using Crest.Media.Core;
using Crest.Media.Core.Helpers;
using Crest.Media.Deployment;
using Crest.Media.Drivers;
using Crest.Media.Endpoints.Api;
using Crest.Media.Events;
using Crest.Media.Fields;
using Crest.Media.Filters;
using Crest.Media.Handlers;
using Crest.Media.Hubs;
using Crest.Media.Indexing;
using Crest.Media.Liquid;
using Crest.Media.Middleware;
using Crest.Media.Processing;
using Crest.Media.Recipes;
using Crest.Media.Services;
using Crest.Media.Settings;
using Crest.Media.Shortcodes;
using Crest.Media.TagHelpers;
using Crest.Media.ViewModels;
using Crest.Modules;
using Crest.Modules.FileProviders;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;
using Crest.Settings;
using Crest.Shortcodes;
using tusdotnet;
using tusdotnet.Models;

namespace Crest.Media;

public sealed class Startup : StartupBase
{
    public Startup() { }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<IJSLocalizer, MediaJSLocalizer>();
        services.AddSingleton<IAnchorTag, MediaAnchorTag>();

        // In-memory directory tree cache (built lazily, invalidated on folder mutations).
        services.AddSingleton<MediaDirectoryTreeCache>();

        // Resized media and remote media caches cleanups.
        services.AddSingleton<IBackgroundTask, ResizedMediaCacheBackgroundTask>();
        services.AddSingleton<IBackgroundTask, RemoteMediaCacheBackgroundTask>();

        services
            .Configure<TemplateOptions>(o =>
            {
                o.MemberAccessStrategy.Register<DisplayMediaFieldViewModel>();
                o.MemberAccessStrategy.Register<Anchor>();

                o.Filters.AddFilter("img_tag", MediaFilters.ImgTag);
            })
            .AddLiquidFilter<AssetUrlFilter>("asset_url")
            .AddLiquidFilter<ResizeUrlFilter>("resize_url");

        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();

        services.AddTransient<IConfigureOptions<MediaOptions>, MediaOptionsConfiguration>();
        services.AddSingleton<IValidateOptions<MediaOptions>, MediaOptionsValidator>();

        // Builds the "MediaApi" authorization policy from MediaApiSettings (cookie default / bearer).
        services.AddTransient<IConfigureOptions<AuthorizationOptions>, MediaApiAuthorizationOptionsConfiguration>();
        services.AddSiteDisplayDriver<MediaApiSettingsDisplayDriver>();
        services.TryAddTransient<FileCreationService>();
        services.AddTransient<FileSizeHelper>();

        services.AddSingleton<IMediaFileProvider>(serviceProvider =>
        {
            var shellOptions = serviceProvider.GetRequiredService<IOptions<ShellOptions>>();
            var shellSettings = serviceProvider.GetRequiredService<ShellSettings>();
            var options = serviceProvider.GetRequiredService<IOptions<MediaOptions>>().Value;

            var mediaPath = GetMediaPath(shellOptions.Value, shellSettings, options.AssetsPath);

            if (!Directory.Exists(mediaPath))
            {
                Directory.CreateDirectory(mediaPath);
            }

            return new MediaFileProvider(options.AssetsRequestPath, mediaPath);
        });

        services.AddSingleton<IStaticFileProvider, IMediaFileProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<IMediaFileProvider>()
        );

        services.AddSingleton<IMediaFileStore>(serviceProvider =>
        {
            var shellOptions = serviceProvider.GetRequiredService<IOptions<ShellOptions>>();
            var shellSettings = serviceProvider.GetRequiredService<ShellSettings>();
            var mediaOptions = serviceProvider.GetRequiredService<IOptions<MediaOptions>>().Value;
            var mediaEventHandlers = serviceProvider.GetServices<IMediaEventHandler>();
            var fileSizeHelper = serviceProvider.GetService<FileSizeHelper>();
            var mediaCreatingEventHandlers =
                serviceProvider.GetServices<IMediaCreatingEventHandler>();
            var fileSystemStoreLogger = serviceProvider.GetRequiredService<
                ILogger<FileSystemStore>
            >();
            var defaultMediaFileStoreLogger = serviceProvider.GetRequiredService<
                ILogger<DefaultMediaFileStore>
            >();

            var mediaPath = GetMediaPath(
                shellOptions.Value,
                shellSettings,
                mediaOptions.AssetsPath
            );
            var fileStore = new FileSystemStore(mediaPath, fileSystemStoreLogger);

            var mediaUrlBase =
                "/"
                + fileStore.Combine(shellSettings.RequestUrlPrefix, mediaOptions.AssetsRequestPath);

            var originalPathBase =
                serviceProvider
                    .GetRequiredService<IHttpContextAccessor>()
                    .HttpContext?.Features.Get<ShellContextFeature>()
                    ?.OriginalPathBase
                ?? PathString.Empty;

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
                defaultMediaFileStoreLogger
            );
        });

        services.AddPermissionProvider<PermissionProvider>();
        services.AddScoped<IAuthorizationHandler, ManageMediaFolderAuthorizationHandler>();
        services.AddNavigationProvider<AdminMenu>();

        // Image processing pipeline (NetVips-based)
        services.AddSingleton<IImageProcessingEngine, VipsImageProcessingEngine>();
        services.AddSingleton<IResizedImageCache, PhysicalFileSystemResizedImageCache>();
        services.AddSingleton<MediaCommandParser>();
        // Shared single-flight instance so concurrent cold-cache requests for the same resized image
        // coalesce into a single transform (cache-stampede protection). Must be a singleton because
        // the middleware itself is transient.
        services.AddSingleton<SingleFlight<string, string>>();
        services.AddTransient<MediaImageProcessingMiddleware>();

        services.AddScoped<MediaTokenSettingsUpdater>();
        services.AddSingleton<IMediaTokenService, MediaTokenService>();
        services.AddTransient<
            IConfigureOptions<MediaTokenOptions>,
            MediaTokenOptionsConfiguration
        >();
        services.AddScoped<IFeatureEventHandler>(sp =>
            sp.GetRequiredService<MediaTokenSettingsUpdater>()
        );
        services.AddScoped<IModularTenantEvents>(sp =>
            sp.GetRequiredService<MediaTokenSettingsUpdater>()
        );

        // Media Field
        services.AddContentField<MediaField>()
            .UseDisplayDriver<MediaFieldDisplayDriver>()
            .AddHandler<AttachedMediaFieldHandler>();
        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, MediaFieldSettingsDriver>();
        services.AddScoped<AttachedMediaFieldFileService, AttachedMediaFieldFileService>();
        services.AddScoped<IContentHandler, AttachedMediaFieldContentHandler>();
        services.AddScoped<IModularTenantEvents, TempDirCleanerService>();
        services.AddScoped<IModularTenantEvents, MediaConfigurationValidator>();
        services.AddScoped<MoveAttachedMediaFieldsStepExecutor>();
        services.AddDataMigration<Migrations>();
        services.AddRecipeExecutionStep<MediaStep>();
        services.AddRecipeExecutionStep<MoveAttachedMediaFieldsStep>();

        // MIME types
        services.TryAddSingleton<IContentTypeProvider, FileExtensionContentTypeProvider>();

        services.AddTagHelpers<ImageTagHelper>();
        services.AddTagHelpers<ImageResizeTagHelper>();
        services.AddTagHelpers<AnchorTagHelper>();

        // Media Profiles
        services.AddScoped<MediaProfilesManager>();
        services.AddScoped<IMediaProfileService, MediaProfileService>();
        services.AddRecipeExecutionStep<MediaProfileStep>();

        // Media Name Normalizer
        services.AddScoped<IMediaNameNormalizerService, NullMediaNameNormalizerService>();

        services.AddScoped<IUserAssetFolderNameProvider, DefaultUserAssetFolderNameProvider>();
        services.AddChunkFileUploadServices();
    }

    public override void Configure(
        IApplicationBuilder app,
        IEndpointRouteBuilder routes,
        IServiceProvider serviceProvider
    )
    {
        routes.AddGetLocalizationsEndpoint()
            .AddGetPermittedStorageEndpoint()
            .AddGetDirectoryTreeEndpoint()
            .AddGetFoldersEndpoint()
            .AddGetMediaItemsEndpoint()
            .AddGetDirectoryContentEndpoint()
            .AddGetMediaItemEndpoint()
            .AddGetMediaFieldItemsEndpoint()
            .AddGetAllMediaItemsEndpoint()
            .AddCopyMediaEndpoint()
            .AddDeleteFolderEndpoint()
            .AddDeleteMediaEndpoint()
            .AddMoveMediaEndpoint()
            .AddDeleteMediaListEndpoint()
            .AddMoveMediaListEndpoint()
            .AddCreateFolderEndpoint()
            .AddUploadMediaEndpoint();

        var mediaFileProvider = serviceProvider.GetRequiredService<IMediaFileProvider>();
        var mediaOptions = serviceProvider.GetRequiredService<IOptions<MediaOptions>>().Value;
        var mediaFileStoreCache = serviceProvider.GetService<IMediaFileStoreCache>();

        // FileStore middleware before image processing, but only if a remote storage module has registered a cache provider.
        if (mediaFileStoreCache != null)
        {
            app.UseMiddleware<MediaFileStoreResolverMiddleware>();
        }

        // Image processing middleware before the static file provider.
        app.UseMiddleware<MediaImageProcessingMiddleware>();

        // The file provider is a circular dependency and replaceable via di.
        mediaOptions.StaticFileOptions.FileProvider = mediaFileProvider;

        // Use services.PostConfigure<MediaOptions>() to alter the media static file options event handlers.
        app.UseStaticFiles(mediaOptions.StaticFileOptions);

    }

    internal static string GetMediaPath(
        ShellOptions shellOptions,
        ShellSettings shellSettings,
        string assetsPath
    )
    {
        assetsPath = assetsPath?.TrimEnd(PathExtensions.PathSeparators);

        if (!MediaFileStorePathHelper.IsValidRelativePath(assetsPath))
        {
            throw new ArgumentException("The media assets path must be a relative subdirectory of the tenant's data directory.", nameof(assetsPath));
        }

        return Path.GetFullPath(Path.Combine(
            shellOptions.ShellsApplicationDataPath,
            shellOptions.ShellsContainerName,
            shellSettings.Name,
            assetsPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)
        ));
    }
}

[Feature("Crest.Media.Cache")]
public sealed class MediaCacheStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<MediaCachePermissions>();
        services.AddNavigationProvider<MediaCacheAdminMenu>();
    }
}

[Feature("Crest.Media.Slugify")]
public sealed class MediaSlugifyStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<MediaSlugifyOptions>, MediaSlugifyOptionsConfiguration>();

        // Media Name Normalizer
        services.AddScoped<IMediaNameNormalizerService, SlugifyMediaNameNormalizerService>();
    }
}

[RequireFeatures("Crest.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<
            MediaDeploymentSource,
            MediaDeploymentStep,
            MediaDeploymentStepDriver
        >();
        services.AddDeployment<
            AllMediaProfilesDeploymentSource,
            AllMediaProfilesDeploymentStep,
            AllMediaProfilesDeploymentStepDriver
        >();
    }
}

[Feature("Crest.Media.Indexing")]
public sealed class MediaIndexingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentFieldIndexHandler, MediaFieldIndexHandler>();
    }
}

[Feature("Crest.Media.Indexing.Text")]
public sealed class TextIndexingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMediaFileTextProvider<TextMediaFileTextProvider>(".txt");
        services.AddMediaFileTextProvider<TextMediaFileTextProvider>(".md");
    }
}

[RequireFeatures("Crest.Shortcodes")]
public sealed class ShortcodesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Only add image as a descriptor as [media] is deprecated.
        services.AddShortcode<ImageShortcodeProvider>(
            "image",
            d =>
            {
                d.DefaultValue = "[image] [/image]";
                d.Hint = "Add a image from the media library.";
                d.Usage =
                    @"[image]foo.jpg[/image]<br>
<table>
  <tr>
    <td>Args:</td>
    <td>width, height, mode</td>
  </tr>
  <tr>
    <td></td>
    <td>class, alt</td>
  </tr>
</table>";
                d.Categories = ["HTML Content", "Media"];
            }
        );

        services.AddShortcode<AssetUrlShortcodeProvider>(
            "asset_url",
            d =>
            {
                d.DefaultValue = "[asset_url] [/asset_url]";
                d.Hint = "Return a url from the media library.";
                d.Usage =
                    @"[asset_url]foo.jpg[/asset_url]<br>
<table>
  <tr>
    <td>Args:</td>
    <td>width, height, mode</td>
  </tr>
</table>";
                d.Categories = ["HTML Content", "Media"];
            }
        );
    }
}

[Feature("Crest.Media.Security")]
public sealed class SecureMediaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Marker service to easily detect if the feature has been enabled.
        services.AddSingleton<SecureMediaMarker>();
        services.AddPermissionProvider<SecureMediaPermissions>();
        services.AddScoped<IAuthorizationHandler, ViewMediaFolderAuthorizationHandler>();

        services.AddSingleton<IMediaEventHandler, SecureMediaFileStoreEventHandler>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseMiddleware<SecureMediaMiddleware>();
    }
}

// This startup is required to ensure that the SecureMediaFeatureEventHandler is registered all the time.
[RequiredStartup]
public sealed class FeatureEventHandlerStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IFeatureEventHandler, SecureMediaFeatureEventHandler>();
    }
}

[Feature("Crest.Media.Tus")]
public sealed class MediaTusStartup : StartupBase
{
    // Run very early so the size-limit middleware executes before anything
    // else in the tenant pipeline can attempt to read the request body.
    public override int Order => PlatformConstants.ConfigureOrder.ReverseProxy - 10;

    public override void ConfigureServices(IServiceCollection services)
    {
        // Marker service so views/controllers can detect TUS is enabled.
        services.AddSingleton<MediaTusMarker>();

        // Default temp store (local disk). Cloud modules replace this via ITusTempStore.
        services.TryAddSingleton<ITusTempStore, DiskTusTempStore>();

        // Distributed store and metadata — works in-memory by default, Redis when available.
        services.AddSingleton<DistributedMediaTusStore>();
        services.AddSingleton<DistributedTusUploadMetadataStore>();
        services.AddSingleton<DistributedFileLockProvider>();
    }

    public override void Configure(
        IApplicationBuilder app,
        IEndpointRouteBuilder routes,
        IServiceProvider serviceProvider
    )
    {
        // Disable Kestrel/IIS request body size limit AND ASP.NET Core form size
        // limit for TUS uploads. This MUST run as early middleware — before any
        // other middleware reads the request body — otherwise the server rejects
        // the request with "Request body too large".
        // tusdotnet enforces its own size limit via MaxAllowedUploadSizeInBytesLong.
        app.Use(
            async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api/media/tus"))
                {
                    // Disable Kestrel's MaxRequestBodySize.
                    var maxRequestBodySizeFeature =
                        context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                    if (maxRequestBodySizeFeature != null && !maxRequestBodySizeFeature.IsReadOnly)
                    {
                        maxRequestBodySizeFeature.MaxRequestBodySize = null;
                    }

                    // Override the form feature so that if anything downstream tries to
                    // read the request as a form, the body length limit won't block it.
                    var formFeature = context.Features.Get<IFormFeature>();
                    if (formFeature == null || formFeature.Form == null)
                    {
                        context.Features.Set<IFormFeature>(
                            new FormFeature(
                                context.Request,
                                new FormOptions { MultipartBodyLengthLimit = long.MaxValue }
                            )
                        );
                    }
                }

                await next();
            }
        );

        // The TusFileInfo endpoint depends on DistributedTusUploadMetadataStore, which is only
        // registered by this feature, so it must be mapped here (not in the base Media feature).
        routes.AddGetTusFileInfoEndpoint();

        routes.MapTus(
            "/api/media/tus",
            async httpContext =>
            {
                // Authenticate against the configured Media API scheme (cookie by default, bearer
                // "Api" when enabled) and adopt its principal so this handler and the tus event
                // callbacks below authorize against that identity.
                var mediaApiSettings = httpContext.RequestServices
                    .GetRequiredService<ISiteService>()
                    .GetSettings<MediaApiSettings>();

                var authenticationScheme = mediaApiSettings.AuthenticationScheme == MediaApiAuthenticationScheme.Bearer
                    ? PlatformConstants.AuthenticationSchemes.Api
                    : IdentityConstants.ApplicationScheme;

                var authenticateResult = await httpContext.AuthenticateAsync(authenticationScheme);
                if (!authenticateResult.Succeeded)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return null;
                }

                httpContext.User = authenticateResult.Principal;

                var authService =
                    httpContext.RequestServices.GetRequiredService<IAuthorizationService>();
                if (
                    !await authService.AuthorizeAsync(
                        httpContext.User,
                        MediaPermissions.ManageMedia
                    )
                )
                {
                    httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return null;
                }
                var canUploadRestrictedMedia = await authService.AuthorizeAsync(
                    httpContext.User,
                    MediaPermissions.UploadRestrictedMedia
                );

                var store =
                    httpContext.RequestServices.GetRequiredService<DistributedMediaTusStore>();
                var mediaOptions = httpContext.RequestServices.GetRequiredService<
                    IOptions<MediaOptions>
                >();
                var mediaFileStore =
                    httpContext.RequestServices.GetRequiredService<IMediaFileStore>();
                var mediaNameNormalizerService =
                    httpContext.RequestServices.GetService<IMediaNameNormalizerService>();
                var fileLockProvider =
                    httpContext.RequestServices.GetRequiredService<DistributedFileLockProvider>();

                return new DefaultTusConfiguration
                {
                    Store = store,
                    FileLockProvider = fileLockProvider,
                    MaxAllowedUploadSizeInBytesLong = mediaOptions.Value.MaxFileSize,
                    Expiration = new tusdotnet.Models.Expiration.SlidingExpiration(
                        mediaOptions.Value.TemporaryFileLifetime
                    ),
                    Events = new tusdotnet.Models.Configuration.Events
                    {
                        OnBeforeCreateAsync = async ctx =>
                        {
                            // Validate file extension and destination path from metadata.
                            var metadata = ctx.Metadata;

                            if (!metadata.TryGetValue("fileName", out var fileNameMeta))
                            {
                                ctx.FailRequest("Missing required metadata: fileName");
                                return;
                            }

                            var fileName = fileNameMeta.GetString(System.Text.Encoding.UTF8);
                            if (mediaNameNormalizerService != null)
                            {
                                fileName = mediaNameNormalizerService.NormalizeFileName(fileName);
                            }
                            fileName = MediaEndpointHelpers.GetFileName(mediaFileStore, fileName);
                            var extension = Path.GetExtension(fileName);

                            if (!mediaOptions.Value.IsFileExtensionAllowed(extension, canUploadRestrictedMedia))
                            {
                                ctx.FailRequest($"File extension not allowed: {extension}");
                                return;
                            }

                            // Validate folder permissions if destinationPath is provided.
                            if (metadata.TryGetValue("destinationPath", out var destMeta))
                            {
                                var destinationPath = destMeta.GetString(System.Text.Encoding.UTF8);
                                if (
                                    !await authService.AuthorizeAsync(
                                        httpContext.User,
                                        MediaPermissions.ManageMediaFolder,
                                        (object)destinationPath
                                    )
                                )
                                {
                                    ctx.FailRequest(
                                        "You do not have permission to upload to this folder."
                                    );
                                    return;
                                }
                            }
                        },

                        OnCreateCompleteAsync = async ctx =>
                        {
                            // Store upload metadata for later retrieval.
                            var metadata = ctx.Metadata;

                            var fileName = metadata.TryGetValue("fileName", out var fileNameMeta)
                                ? fileNameMeta.GetString(System.Text.Encoding.UTF8)
                                : string.Empty;
                            var destinationPath = metadata.TryGetValue(
                                "destinationPath",
                                out var destMeta
                            )
                                ? destMeta.GetString(System.Text.Encoding.UTF8)
                                : string.Empty;

                            // Normalize file name if the service is available.
                            if (mediaNameNormalizerService != null)
                            {
                                fileName = mediaNameNormalizerService.NormalizeFileName(fileName);
                            }
                            fileName = MediaEndpointHelpers.GetFileName(mediaFileStore, fileName);

                            var metadataStore =
                                httpContext.RequestServices.GetRequiredService<DistributedTusUploadMetadataStore>();
                            await metadataStore.SetAsync(
                                ctx.FileId,
                                new TusUploadEntry
                                {
                                    DestinationPath = destinationPath,
                                    FileName = fileName,
                                },
                                ctx.CancellationToken
                            );
                        },

                        OnFileCompleteAsync = async ctx =>
                        {
                            // Move completed file from temp to IMediaFileStore.
                            var metadataStore =
                                httpContext.RequestServices.GetRequiredService<DistributedTusUploadMetadataStore>();
                            var entry = await metadataStore.GetAsync(
                                ctx.FileId,
                                ctx.CancellationToken
                            );

                            if (entry == null)
                            {
                                return;
                            }

                            if (
                                !await authService.AuthorizeAsync(
                                    httpContext.User,
                                    MediaPermissions.ManageMediaFolder,
                                    (object)entry.DestinationPath
                                )
                                || !mediaOptions.Value.IsFileExtensionAllowed(
                                    Path.GetExtension(entry.FileName),
                                    canUploadRestrictedMedia
                                )
                            )
                            {
                                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                                return;
                            }

                            var mediaFilePath = await GetAvailableMediaFilePathAsync(
                                mediaFileStore,
                                entry.DestinationPath,
                                entry.FileName
                            );

                            using var stream = store.OpenReadStream(ctx.FileId);
                            var finalPath = await mediaFileStore.CreateFileFromStreamAsync(
                                mediaFilePath,
                                stream
                            );
                            entry.MediaFilePath = finalPath;

                            // Persist the updated entry (with MediaFilePath) back to the distributed cache
                            // so GetTusFileInfo can retrieve it.
                            await metadataStore.SetAsync(ctx.FileId, entry, ctx.CancellationToken);

                            // Clean up the temp file.
                            await store.DeleteFileAsync(ctx.FileId, ctx.CancellationToken);
                        },
                    },
                };
            }
        );
    }

    private static async Task<string> GetAvailableMediaFilePathAsync(
        IMediaFileStore mediaFileStore,
        string destinationPath,
        string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var baseFileName = Path.GetFileNameWithoutExtension(fileName);

        if (string.IsNullOrEmpty(baseFileName))
        {
            baseFileName = "file";
        }

        var filePath = mediaFileStore.Combine(destinationPath, fileName);

        if (await mediaFileStore.GetFileInfoAsync(filePath) == null)
        {
            return filePath;
        }

        var index = 1;
        do
        {
            filePath = mediaFileStore.Combine(destinationPath, $"{baseFileName}-{index}{extension}");
            index++;
        }
        while (await mediaFileStore.GetFileInfoAsync(filePath) != null);

        return filePath;
    }
}

[Feature("Crest.Media.SignalR")]
public sealed class MediaSignalRStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMediaEventHandler, MediaSignalREventHandler>();
    }

    public override void Configure(
        IApplicationBuilder app,
        IEndpointRouteBuilder routes,
        IServiceProvider serviceProvider)
    {
        routes.MapHub<MediaHub>("/hubs/media");
    }
}
