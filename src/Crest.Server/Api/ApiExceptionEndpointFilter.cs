using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Api;

/// <summary>
/// The error contract on the registry's route groups (docs/MVC/api.md › The error contract): an
/// exception a handler throws is answered by the first <see cref="IApiExceptionMapper"/> that maps
/// it; one no mapper knows propagates to the host's error handling.
/// </summary>
internal sealed class ApiExceptionEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception) when (Map(exception, context.HttpContext) is { } result)
        {
            return result;
        }
    }

    internal static IResult? Map(Exception exception, HttpContext httpContext)
    {
        foreach (var mapper in httpContext.RequestServices.GetServices<IApiExceptionMapper>())
        {
            if (mapper.Map(exception, httpContext) is { } result)
            {
                return result;
            }
        }

        return null;
    }
}
