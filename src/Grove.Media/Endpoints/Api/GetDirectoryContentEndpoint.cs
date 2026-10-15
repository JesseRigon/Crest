using Grove.Api;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using Grove.Media.Services;
using Grove.Media.ViewModels;

namespace Grove.Media.Endpoints.Api;

public static class GetDirectoryContentEndpoint
{
    public static IEndpointRouteBuilder AddGetDirectoryContentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("GetDirectoryContent", HandleAsync)
            .WithName("ApiGetDirectoryContent")
            .DisableAntiforgery()
            .Produces<DirectoryContentDto>(StatusCodes.Status200OK)
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
        [FromServices] IOptions<MediaOptions> options,
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

        // Only check directory existence for non-root paths (root always exists).
        if (path.Length > 0 && await mediaFileStore.GetDirectoryInfoAsync(path) == null)
        {
            return httpContext.ApiNotFoundProblem();
        }

        var canUploadRestrictedMedia = await authorizationService.AuthorizeAsync(
            httpContext.User,
            MediaPermissions.UploadRestrictedMedia);

        // Fetch folders and files concurrently.
        var foldersTask = MediaEndpointHelpers.GetDirectoryFoldersAsync(mediaFileStore, authorizationService, httpContext.User, path);
        var filesTask = MediaEndpointHelpers.GetDirectoryFilesAsync(
            mediaFileStore,
            httpContext,
            contentTypeProvider,
            fileVersionProvider,
            options.Value,
            canUploadRestrictedMedia,
            path,
            extensions);

        await Task.WhenAll(foldersTask, filesTask);

        return TypedResults.Ok(new DirectoryContentDto
        {
            Folders = foldersTask.Result,
            Files = filesTask.Result,
        });
    }
}
