#nullable enable

using Grove.Api;
using Grove.Contents.Management.Handlers;
using Microsoft.AspNetCore.Http;

namespace Grove.Contents.Api;

/// <summary>
/// A save a save guard refused (a picker's allowed types, a relationship rule) answers the error
/// contract's 400, the guard's messages under the object's key <c>""</c> (docs/MVC/api.md › The
/// error contract).
/// </summary>
public sealed class ContentSaveRefusedExceptionMapper : IApiExceptionMapper
{
    public IResult? Map(Exception exception, HttpContext httpContext) =>
        exception is ContentSaveRefusedException refused
            ? ApiProblems.Validation(new Dictionary<string, string[]> { [string.Empty] = [.. refused.Errors] }, detail: refused.Message)
            : null;
}
