using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Api;

/// <summary>
/// Antiforgery for the registry's route groups (docs/MVC/api.md › Hosting, H1): a state-changing
/// call validates the <c>RequestVerificationToken</c> header. The tenant's <see cref="IAntiforgery"/>
/// is Access's: it passes bearer callers, whose credential a browser never sends on its own. An
/// endpoint opts out with <c>.SkipApiAntiforgery()</c>; the framework's own antiforgery metadata
/// (<c>.DisableAntiforgery()</c>, which form-binding endpoints need without the framework's
/// middleware) does not exempt it.
/// </summary>
internal sealed class ApiAntiforgeryEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (RequiresValidation(httpContext))
        {
            var antiforgery = httpContext.RequestServices.GetRequiredService<IAntiforgery>();

            try
            {
                await antiforgery.ValidateRequestAsync(httpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return TypedResults.Problem(
                    title: "Bad request",
                    detail: "The antiforgery token was missing or invalid.",
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }

        return await next(context);
    }

    internal static bool RequiresValidation(HttpContext httpContext)
    {
        var method = httpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return false;
        }

        return httpContext.GetEndpoint()?.Metadata.GetMetadata<SkipApiAntiforgeryMetadata>() is null;
    }
}
