using Microsoft.AspNetCore.Http;

namespace Crest.Api;

/// <summary>
/// The error contract's shapes for handlers (docs/MVC/api.md › The error contract): RFC 9457 problem
/// details, with an <c>errors</c> map keyed by the member's wire name for validation: the same shape
/// ASP.NET's native validation answers a request DTO's rule failures with. The 400,
/// 403 and 404 helpers without an errors map are <c>ProblemDetailsApiControllerExtensions</c>'
/// <see cref="HttpContext"/> overloads.
/// </summary>
public static class ApiProblems
{
    public static IResult Validation(IDictionary<string, string[]> errors, string? detail = null, string? title = null) =>
        TypedResults.ValidationProblem(errors, detail: detail, title: title ?? "A validation error occurred.");

    public static IResult Validation(string key, string message) =>
        Validation(new Dictionary<string, string[]> { [key] = [message] });

    /// <summary>The errors a handler's own rules collected (<see cref="Crest.Validation.ValidationErrors"/>).</summary>
    public static IResult Validation(Crest.Validation.ValidationErrors errors) => Validation(errors.ToDictionary());

    public static IResult Conflict(string? detail, string? title = null) =>
        TypedResults.Problem(detail: detail, title: title ?? "Conflict", statusCode: StatusCodes.Status409Conflict);

    public static IResult BadRequest(string? detail, string? title = null) =>
        TypedResults.Problem(detail: detail, title: title ?? "Bad request", statusCode: StatusCodes.Status400BadRequest);

    public static IResult NotFound(string? detail = null, string? title = null) =>
        TypedResults.Problem(detail: detail, title: title ?? "Not Found", statusCode: StatusCodes.Status404NotFound);
}
