using Grove.Api;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Grove.Media.ViewModels;

namespace Grove.Media.Endpoints.Api;

public static class GetMediaItemEndpoint
{
    public static IEndpointRouteBuilder AddGetMediaItemEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("GetMediaItem", HandleAsync)
            .WithName("ApiGetMediaItem")
            .DisableAntiforgery()
            .Produces<FileStoreEntryDto>(StatusCodes.Status200OK)
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
        [FromServices] IContentTypeProvider contentTypeProvider,
        [FromServices] IFileVersionProvider fileVersionProvider,
        [FromQuery] string path)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMedia)
            || !await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)(path ?? string.Empty)))
        {
            return httpContext.ApiForbidProblem();
        }

        if (string.IsNullOrEmpty(path))
        {
            return httpContext.ApiNotFoundProblem();
        }

        var fileEntry = await mediaFileStore.GetFileInfoAsync(path);

        if (fileEntry == null)
        {
            return httpContext.ApiNotFoundProblem();
        }

        return TypedResults.Ok(MediaEndpointHelpers.CreateFileResult(fileEntry, httpContext, contentTypeProvider, fileVersionProvider, mediaFileStore));
    }
}
