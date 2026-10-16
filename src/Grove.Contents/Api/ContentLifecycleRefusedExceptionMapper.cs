#nullable enable

using Grove.Api;
using Grove.Contents.Management.Handlers;
using Microsoft.AspNetCore.Http;

namespace Grove.Contents.Api;

/// <summary>
/// A change a content lifecycle hook refused answers the error contract's 400: each error under
/// the field path the hook named (<c>FieldSet.Field</c>, <c>FieldSet</c>, or <c>""</c> for the item;
/// docs/MVC/api.md › The error contract, docs/content-engine.md › Behaviour is workflows).
/// </summary>
public sealed class ContentLifecycleRefusedExceptionMapper : IApiExceptionMapper
{
    public IResult? Map(Exception exception, HttpContext httpContext) =>
        exception is ContentLifecycleRefusedException refused
            ? ApiProblems.Validation(ToMap(refused.Errors, key => key), detail: refused.Message)
            : null;

    public static Dictionary<string, string[]> ToMap(IEnumerable<ContentLifecycleError> errors, Func<string, string> key) =>
        errors
            .GroupBy(error => key(error.Path ?? string.Empty), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray(), StringComparer.Ordinal);
}
