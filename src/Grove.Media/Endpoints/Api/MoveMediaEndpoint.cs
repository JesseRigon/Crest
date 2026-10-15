using Grove.Api;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Media.Services;

namespace Grove.Media.Endpoints.Api;

public static class MoveMediaEndpoint
{
    public static IEndpointRouteBuilder AddMoveMediaEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("MoveMedia", HandleAsync)
            .WithName("ApiMoveMedia")
            .DisableAntiforgery()
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return builder;
    }

    [Authorize(Policy = MediaApiConstants.AuthorizationPolicyName)]
    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IMediaFileStore mediaFileStore,
        [FromServices] IOptions<MediaOptions> options,
        [FromServices] IServiceProvider serviceProvider,
        [FromServices] ILogger<MediaApiEndpoints> logger,
        [FromServices] IStringLocalizer<MediaApiEndpoints> localizer,
        [FromQuery] string oldPath,
        [FromQuery] string newPath)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMedia)
            || !await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)oldPath)
            || !await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)newPath))
        {
            return httpContext.ApiForbidProblem();
        }

        if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
        {
            return httpContext.ApiNotFoundProblem();
        }

        if (await mediaFileStore.GetFileInfoAsync(oldPath) == null)
        {
            return httpContext.ApiNotFoundProblem();
        }

        var newExtension = Path.GetExtension(newPath);
        var canUploadRestrictedMedia = await authorizationService.AuthorizeAsync(
            httpContext.User,
            MediaPermissions.UploadRestrictedMedia);

        if (!options.Value.IsFileExtensionAllowed(newExtension, canUploadRestrictedMedia))
        {
            return httpContext.ApiValidationProblem(detail: localizer["This file extension is not allowed: {0}", newExtension]);
        }

        if (await mediaFileStore.GetFileInfoAsync(newPath) != null)
        {
            return httpContext.ApiValidationProblem(detail: localizer["Cannot move media because a file already exists with the same name"]);
        }

        await mediaFileStore.MoveFileAsync(oldPath, newPath);

        var movedFile = await mediaFileStore.GetFileInfoAsync(newPath);
        await MediaEndpointHelpers.PreCacheRemoteMediaAsync(
            movedFile,
            mediaFileStore,
            serviceProvider.GetService(typeof(IMediaFileStoreCache)) as IMediaFileStoreCache,
            httpContext,
            logger);

        return TypedResults.Ok();
    }
}
