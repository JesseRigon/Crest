using Microsoft.AspNetCore.Http;

namespace Crest.Api;

/// <summary>
/// Results that keep the bodies the controller-hosted APIs answered with, for handlers converted
/// from them (the API surface does not change in the move, docs/server.md): an error message is
/// a <c>text/plain</c> body, which the admin clients show as is. New APIs answer with
/// <see cref="ApiProblems"/>.
/// </summary>
public static class ApiResults
{
    public static IResult BadRequest(string message) => TypedResults.Text(message, statusCode: StatusCodes.Status400BadRequest);

    public static IResult Conflict(string message) => TypedResults.Text(message, statusCode: StatusCodes.Status409Conflict);

    public static IResult NotFound(string message) => TypedResults.Text(message, statusCode: StatusCodes.Status404NotFound);

    public static IResult Forbidden(string message) => TypedResults.Text(message, statusCode: StatusCodes.Status403Forbidden);

    public static IResult Status(int statusCode, string message) => TypedResults.Text(message, statusCode: statusCode);
}
