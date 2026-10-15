using Grove.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Api;

/// <summary>
/// The validation sink on the registry's route groups (docs/MVC/api.md › The error contract, E3):
/// for an endpoint marked <see cref="ValidationSinkMetadata"/>, a successful result is replaced by
/// the error contract's 400 when the request's <see cref="IValidationSink"/> holds errors. A result
/// that already is an error (a status of 400 or more) is left as it is.
/// </summary>
internal sealed class ApiValidationSinkEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);

        var httpContext = context.HttpContext;
        if (httpContext.GetEndpoint()?.Metadata.GetMetadata<ValidationSinkMetadata>() is null)
        {
            return result;
        }

        if (result is IStatusCodeHttpResult { StatusCode: >= 400 })
        {
            return result;
        }

        var sink = httpContext.RequestServices.GetService<IValidationSink>();
        return sink is { IsValid: false } ? ApiProblems.Validation(sink) : result;
    }
}
