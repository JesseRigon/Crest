using Crest.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Api.Endpoints;

/// <summary>
/// The results the engine's handlers answer with. JSON bodies go through the engine's
/// <see cref="IApiSerializer"/> options (the polymorphic activity JSON), never the host's;
/// errors are the error contract's problem details (<see cref="ApiProblems"/>), the engine's
/// non-field messages under <see cref="GeneralErrorKey"/>; the bare status answers are the
/// engine's (an empty 200, 204, 403, 404 — a 403 is a status, never the scheme's Forbid, which
/// would redirect the API call to a page).
/// </summary>
public static class EngineResults
{
    /// <summary>The <c>errors</c> key of a message that names no field.</summary>
    public const string GeneralErrorKey = "";

    public static IResult Json<T>(HttpContext context, T value, int statusCode = StatusCodes.Status200OK) =>
        TypedResults.Json(value, Options(context), statusCode: statusCode);

    public static IResult Json<T>(HttpContext context, T value, System.Text.Json.JsonSerializerOptions options, int statusCode = StatusCodes.Status200OK) =>
        TypedResults.Json(value, options, statusCode: statusCode);

    public static IResult Ok() => TypedResults.Ok();

    public static IResult NoContent() => TypedResults.NoContent();

    public static IResult NotFound() => TypedResults.NotFound();

    public static IResult Forbidden() => TypedResults.StatusCode(StatusCodes.Status403Forbidden);

    public static IResult Errors(params string[] messages) =>
        ApiProblems.Validation(new Dictionary<string, string[]> { [GeneralErrorKey] = messages });

    public static IResult Errors(IEnumerable<string> messages) => Errors(messages.ToArray());

    public static IResult File(byte[] contents, string fileName, string contentType = "application/octet-stream") =>
        TypedResults.File(contents, contentType, fileName);

    public static System.Text.Json.JsonSerializerOptions Options(HttpContext context) =>
        context.RequestServices.GetRequiredService<IApiSerializer>().GetOptions();
}
