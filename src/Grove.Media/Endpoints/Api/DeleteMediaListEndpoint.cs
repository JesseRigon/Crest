using Grove.Api;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Media.Endpoints.Api;

public static class DeleteMediaListEndpoint
{
    public static IEndpointRouteBuilder AddDeleteMediaListEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("DeleteMediaList", HandleAsync)
            .WithName("ApiDeleteMediaList")
            .DisableAntiforgery()
            .Produces(StatusCodes.Status200OK)
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
        [FromBody] List<string> paths)
    {
        if (paths == null)
        {
            return httpContext.ApiNotFoundProblem();
        }

        if (!await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMedia))
        {
            return httpContext.ApiForbidProblem();
        }

        foreach (var path in paths)
        {
            if (!await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)path))
            {
                return httpContext.ApiForbidProblem();
            }
        }

        foreach (var path in paths)
        {
            if (!await mediaFileStore.TryDeleteFileAsync(path))
            {
                return httpContext.ApiNotFoundProblem();
            }
        }

        return TypedResults.Ok();
    }
}
