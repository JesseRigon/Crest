#nullable enable

using Grove.Api;
using Microsoft.AspNetCore.Http;

namespace Grove.Contents.Types;

/// <summary>A change to a locked definition answers 409 (docs/content-engine.md › Definition locks).</summary>
public sealed class DefinitionLockedExceptionMapper : IApiExceptionMapper
{
    public IResult? Map(Exception exception, HttpContext httpContext) =>
        exception is DefinitionLockedException locked
            ? TypedResults.Problem(detail: locked.Message, statusCode: StatusCodes.Status409Conflict, title: "The definition is locked.")
            : null;
}
