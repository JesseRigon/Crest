using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Crest.ContentManagement;
using Crest.Json;
using Crest.Modules;

namespace Crest.Contents.Endpoints.Api;

public static class GetEndpoint
{
    public static IEndpointRouteBuilder AddGetContentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("api/content/{contentItemId}", HandleAsync)
            .WithName("ApiGetContentItem")
            .AllowAnonymous()
            .DisableAntiforgery();

        return builder;
    }

    [Authorize]
    private static async Task<IResult> HandleAsync(
        string contentItemId,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        HttpContext httpContext,
        IOptions<DocumentJsonSerializerOptions> options)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CommonPermissions.AccessContentApi))
        {
            return httpContext.ChallengeOrForbid(PlatformConstants.AuthenticationSchemes.Api);
        }

        var contentItem = await contentManager.GetAsync(contentItemId);

        if (contentItem == null)
        {
            return TypedResults.NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(httpContext.User, CommonPermissions.ViewContent, contentItem))
        {
            return httpContext.ChallengeOrForbid(PlatformConstants.AuthenticationSchemes.Api);
        }

        return Results.Json(contentItem, options.Value.SerializerOptions);
    }
}
