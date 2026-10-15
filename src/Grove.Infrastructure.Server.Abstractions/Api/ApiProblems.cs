using Microsoft.AspNetCore.Http;

namespace Grove.Api;

/// <summary>
/// The error contract's shapes for handlers (docs/MVC/api.md › The error contract): RFC 9457 problem
/// details, with an <c>errors</c> map keyed by the member's wire name for validation: the same shape
/// ASP.NET's native validation answers a request DTO's rule failures with. The 400,
/// 403 and 404 helpers with localized default titles are <see cref="ApiProblemExtensions"/>'
/// <see cref="HttpContext"/> overloads.
/// </summary>
public static class ApiProblems
{
    public static IResult Validation(IDictionary<string, string[]> errors, string? detail = null, string? title = null) =>
        TypedResults.ValidationProblem(errors, detail: detail, title: title ?? "A validation error occurred.");

    public static IResult Validation(string key, string message) =>
        Validation(new Dictionary<string, string[]> { [key] = [message] });

    /// <summary>The errors a handler's own rules collected (<see cref="Grove.Validation.ValidationErrors"/>).</summary>
    public static IResult Validation(Grove.Validation.ValidationErrors errors) => Validation(errors.ToDictionary());

    /// <summary>
    /// The errors raised below the API layer in this request (the content manager's refusals, the
    /// part and field handlers' validation): the error contract's map, keyed by field path
    /// (<c>Part.Field</c>), setting name or <c>""</c>.
    /// </summary>
    public static IResult Validation(Grove.Validation.IValidationSink sink) => Validation(sink.Errors);

    public static IResult Conflict(string? detail, string? title = null) =>
        TypedResults.Problem(detail: detail, title: title ?? "Conflict", statusCode: StatusCodes.Status409Conflict);

    public static IResult BadRequest(string? detail, string? title = null) =>
        TypedResults.Problem(detail: detail, title: title ?? "Bad request", statusCode: StatusCodes.Status400BadRequest);

    public static IResult NotFound(string? detail = null, string? title = null) =>
        TypedResults.Problem(detail: detail, title: title ?? "Not Found", statusCode: StatusCodes.Status404NotFound);
}
