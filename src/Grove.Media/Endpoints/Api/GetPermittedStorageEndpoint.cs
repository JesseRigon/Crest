using Grove.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Grove.Media.Helpers;
using Grove.Media.ViewModels;

namespace Grove.Media.Endpoints.Api;

public static class GetPermittedStorageEndpoint
{
    public static IEndpointRouteBuilder AddGetPermittedStorageEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("GetPermittedStorage", HandleAsync)
            .WithName("ApiGetPermittedStorage")
            .DisableAntiforgery()
            .Produces<PermittedStorageDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return builder;
    }

    [Authorize(Policy = MediaApiConstants.AuthorizationPolicyName)]
    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IMediaFileStore mediaFileStore,
        [FromServices] IStringLocalizer<MediaApiEndpoints> localizer,
        [FromServices] FileSizeHelper fileSizeHelper)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMedia)
            || !await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)string.Empty))
        {
            return httpContext.ApiForbidProblem();
        }

        var bytes = await mediaFileStore.GetPermittedStorageAsync();
        var text = bytes == null ? localizer["Unspecified"] : fileSizeHelper.FormatSize(bytes.Value);

        return TypedResults.Ok(new PermittedStorageDto { Bytes = bytes, Text = text });
    }
}
