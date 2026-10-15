using Grove.Api;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Grove.Media.Services;
using Grove.Media.ViewModels;

namespace Grove.Media.Endpoints.Api;

public static class GetTusFileInfoEndpoint
{
    public static IEndpointRouteBuilder AddGetTusFileInfoEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("TusFileInfo/{uploadId}", HandleAsync)
            .WithName("ApiGetTusFileInfo")
            .DisableAntiforgery()
            .Produces<FileStoreEntryDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
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
        [FromServices] DistributedTusUploadMetadataStore tusMetadataStore,
        [FromRoute] string uploadId)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMedia))
        {
            return httpContext.ApiForbidProblem();
        }

        var entry = await tusMetadataStore.GetAsync(uploadId);

        if (entry == null || string.IsNullOrEmpty(entry.MediaFilePath))
        {
            return httpContext.ApiNotFoundProblem();
        }

        var fileInfo = await mediaFileStore.GetFileInfoAsync(entry.MediaFilePath);

        if (fileInfo == null)
        {
            return httpContext.ApiNotFoundProblem();
        }

        // Remove the entry now that the client has retrieved it.
        await tusMetadataStore.RemoveAsync(uploadId);

        return TypedResults.Ok(MediaEndpointHelpers.CreateFileResult(fileInfo, httpContext, contentTypeProvider, fileVersionProvider, mediaFileStore));
    }
}
