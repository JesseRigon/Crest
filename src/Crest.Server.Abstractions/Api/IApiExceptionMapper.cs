using Microsoft.AspNetCore.Http;

namespace Crest.Api;

/// <summary>
/// Maps a domain exception thrown by an API handler to the error contract (docs/MVC/api.md › The
/// error contract): an <c>application/problem+json</c> result. Registered by the module that owns the
/// exception; the server's route groups ask every mapper, in registration order, and rethrow an
/// exception none maps.
/// </summary>
public interface IApiExceptionMapper
{
    IResult? Map(Exception exception, HttpContext httpContext);
}
