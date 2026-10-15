using Grove.Api;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Data.Storage;
using Grove.Media.ViewModels;

namespace Grove.Media.Endpoints.Api;

public static class UploadMediaEndpoint
{
    public static IEndpointRouteBuilder AddUploadMediaEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("Upload", HandleAsync)
            .WithName("ApiUploadMedia")
            .DisableAntiforgery()
            .Produces<UploadFilesResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return builder;
    }

    [Authorize(Policy = MediaApiConstants.AuthorizationPolicyName)]
    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IMediaFileStore mediaFileStore,
        [FromServices] IMediaNameNormalizerService mediaNameNormalizerService,
        [FromServices] IContentTypeProvider contentTypeProvider,
        [FromServices] IFileVersionProvider fileVersionProvider,
        [FromServices] IChunkFileUploadService chunkFileUploadService,
        [FromServices] FileCreationService fileCreationService,
        [FromServices] IServiceProvider serviceProvider,
        [FromServices] IOptions<MediaOptions> options,
        [FromServices] ILogger<MediaApiEndpoints> logger,
        [FromServices] IStringLocalizer<MediaApiEndpoints> localizer,
        [FromQuery] string path,
        [FromQuery] string extensions)
    {
        if (string.IsNullOrEmpty(path))
        {
            path = string.Empty;
        }

        if (!await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMedia)
            || !await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)path))
        {
            return httpContext.ApiForbidProblem();
        }

        var mediaOptions = options.Value;
        var canUploadRestrictedMedia = await authorizationService.AuthorizeAsync(
            httpContext.User,
            MediaPermissions.UploadRestrictedMedia);

        // Replicate the [MediaSizeLimit] filter: cap the multipart body / request size using the
        // configured MaxFileSize before the form is read by the chunk upload service.
        ApplyMediaSizeLimit(httpContext, mediaOptions.MaxFileSize);

        var requestedExtensions = MediaEndpointHelpers.GetRequestedExtensions(extensions);

        return await chunkFileUploadService.ProcessRequestAsync(
            httpContext.Request,

            // We need this empty object because the frontend expects a JSON object in the response.
            (_, _, _) => Task.FromResult<IResult>(TypedResults.Ok(new { })),
            async (files) =>
            {
                var result = new List<UploadFileResultDto>();

                // Loop through each file in the request.
                foreach (var file in files)
                {
                    var fileName = MediaEndpointHelpers.GetFileName(
                        mediaFileStore,
                        mediaNameNormalizerService.NormalizeFileName(file.FileName));
                    var extension = Path.GetExtension(fileName);

                    if (!mediaOptions.IsFileExtensionAllowed(extension, canUploadRestrictedMedia)
                        || (requestedExtensions.Count > 0 && !requestedExtensions.Contains(extension)))
                    {
                        result.Add(new UploadFileResultDto
                        {
                            Name = file.FileName,
                            Size = file.Length,
                            Folder = path,
                            Error = localizer["This file extension is not allowed: {0}", extension].ToString(),
                        });

                        if (logger.IsEnabled(LogLevel.Information))
                        {
                            logger.LogInformation("File extension not allowed: '{File}'", file.FileName);
                        }

                        continue;
                    }

                    Stream stream = null;
                    try
                    {
                        var mediaFilePath = mediaFileStore.Combine(path, fileName);

                        if (await mediaFileStore.GetFileInfoAsync(mediaFilePath) != null)
                        {
                            result.Add(new UploadFileResultDto
                            {
                                Name = fileName,
                                Size = file.Length,
                                Folder = path,
                                Error = localizer["A file with this name already exists in the current folder."].ToString(),
                            });

                            continue;
                        }

                        stream = file.OpenReadStream();
                        mediaFilePath = await mediaFileStore.CreateFileFromStreamAsync(
                            fileCreationService,
                            mediaFilePath,
                            stream,
                            length: file.Length,
                            contentType: file.ContentType,
                            cancellationToken: httpContext.RequestAborted);

                        var mediaFile = await mediaFileStore.GetFileInfoAsync(mediaFilePath);

                        await MediaEndpointHelpers.PreCacheRemoteMediaAsync(
                            mediaFile,
                            mediaFileStore,
                            serviceProvider.GetService(typeof(IMediaFileStoreCache)) as IMediaFileStoreCache,
                            httpContext,
                            logger);

                        result.Add(new UploadFileResultDto(MediaEndpointHelpers.CreateFileResult(mediaFile, httpContext, contentTypeProvider, fileVersionProvider, mediaFileStore)));
                    }
                    catch (ExistsFileStoreException ex)
                    {
                        logger.LogWarning(ex, "An error occurred while uploading a media");

                        result.Add(new UploadFileResultDto
                        {
                            Name = fileName,
                            Size = file.Length,
                            Folder = path,
                            Error = ex.Message,
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error occurred while uploading a media");

                        result.Add(new UploadFileResultDto
                        {
                            Name = fileName,
                            Size = file.Length,
                            Folder = path,
                            Error = ex.Message,
                        });
                    }
                    finally
                    {
                        stream?.Dispose();
                    }
                }

                return (IResult)TypedResults.Ok(new UploadFilesResultDto { Files = result });
            });
    }

    // Mirrors Grove.Media.Services.MediaSizeLimitAttribute's InternalMediaSizeFilter, applied
    // inline because minimal-API endpoints do not run MVC authorization filters.
    private static void ApplyMediaSizeLimit(HttpContext httpContext, long maxFileSize)
    {
        var features = httpContext.Features;
        var formFeature = features.Get<IFormFeature>();

        if (formFeature == null || formFeature.Form == null)
        {
            // Request form has not been read yet, so set the limits.
            features.Set<IFormFeature>(new FormFeature(httpContext.Request, new FormOptions
            {
                MultipartBodyLengthLimit = maxFileSize,
            }));
        }

        // Will only be available when running OutOfProcess with Kestrel.
        var maxRequestBodySizeFeature = features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxRequestBodySizeFeature != null && !maxRequestBodySizeFeature.IsReadOnly)
        {
            maxRequestBodySizeFeature.MaxRequestBodySize = maxFileSize;
        }
    }
}
