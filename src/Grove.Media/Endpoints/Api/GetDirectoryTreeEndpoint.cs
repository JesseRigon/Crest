using Grove.Api;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Grove.Data.Storage;
using Grove.Media.Services;
using Grove.Media.ViewModels;

namespace Grove.Media.Endpoints.Api;

public static class GetDirectoryTreeEndpoint
{
    public static IEndpointRouteBuilder AddGetDirectoryTreeEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("GetDirectoryTree", HandleAsync)
            .WithName("ApiGetDirectoryTree")
            .DisableAntiforgery()
            .Produces<DirectoryTreeNodeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return builder;
    }

    [Authorize(Policy = MediaApiConstants.AuthorizationPolicyName)]
    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IMediaFileStore mediaFileStore,
        [FromServices] IOptions<MediaOptions> options,
        [FromServices] IUserAssetFolderNameProvider userAssetFolderNameProvider,
        [FromServices] MediaDirectoryTreeCache directoryTreeCache)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMedia)
            || !await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)string.Empty))
        {
            return httpContext.ApiForbidProblem();
        }

        var mediaOptions = options.Value;

        // Create default user folder if needed.
        if (await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageOwnMedia)
            && await mediaFileStore.GetDirectoryInfoAsync(mediaFileStore.Combine(mediaOptions.AssetsUsersFolder, userAssetFolderNameProvider.GetUserAssetFolderName(httpContext.User))) == null)
        {
            await mediaFileStore.TryCreateDirectoryAsync(mediaFileStore.Combine(mediaOptions.AssetsUsersFolder, userAssetFolderNameProvider.GetUserAssetFolderName(httpContext.User)));
        }

        var cached = await directoryTreeCache.GetTreeAsync();

        return TypedResults.Ok(await MediaEndpointHelpers.ToDtoAsync(authorizationService, httpContext.User, cached));
    }
}
