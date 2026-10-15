#nullable enable

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Grove.Api;

/// <summary>The localization context of the error contract's default titles and details.</summary>
public sealed class ApiProblemLocalization;

/// <summary>
/// The error contract's problem details with localized default titles, for handlers that answer
/// with the request's <see cref="HttpContext"/> at hand (docs/MVC/api.md › The error contract).
/// </summary>
public static class ApiProblemExtensions
{
    /// <summary>
    /// A 403 problem for a failed permission check, regardless of the authentication scheme:
    /// unlike a forbid, which under the cookie scheme redirects to the HTML access-denied page,
    /// it always answers a problem details body. Never a 401: that would bypass the authentication
    /// handlers and omit the <c>WWW-Authenticate</c> header RFC 9110 §15.5.2 requires; callers
    /// require authorization so an anonymous request is challenged before the handler runs.
    /// </summary>
    public static IResult ApiForbidProblem(this HttpContext httpContext)
    {
        var S = Localizer(httpContext);
        return TypedResults.Problem(
            title: S["Forbidden"],
            detail: S["You do not have sufficient permissions to complete this request"],
            statusCode: StatusCodes.Status403Forbidden);
    }

    /// <summary>A 400 problem.</summary>
    public static IResult ApiBadRequestProblem(this HttpContext httpContext, string? detail = null, string? title = null) =>
        TypedResults.Problem(title: string.IsNullOrEmpty(title) ? Localizer(httpContext)["Bad request"] : title, detail: detail, statusCode: StatusCodes.Status400BadRequest);

    /// <summary>A 404 problem.</summary>
    public static IResult ApiNotFoundProblem(this HttpContext httpContext, string? detail = null, string? title = null) =>
        TypedResults.Problem(title: string.IsNullOrEmpty(title) ? Localizer(httpContext)["Not Found"] : title, detail: detail, statusCode: StatusCodes.Status404NotFound);

    /// <summary>A 400 validation problem with the error contract's <c>errors</c> map (empty when none is given).</summary>
    public static IResult ApiValidationProblem(this HttpContext httpContext, string? detail = null, string? title = null, IDictionary<string, string[]>? errors = null) =>
        TypedResults.ValidationProblem(
            errors ?? new Dictionary<string, string[]>(),
            detail: detail,
            title: string.IsNullOrEmpty(title) ? Localizer(httpContext)["A validation error occurred."] : title);

    private static IStringLocalizer Localizer(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<IStringLocalizer<ApiProblemLocalization>>();
}
