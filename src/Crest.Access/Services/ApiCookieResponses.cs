using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Crest.Access.Services;

/// <summary>
/// An API path never redirects (docs/access.md § One gate): on an <see cref="AccessGate.IsApiPath"/>
/// request the identity cookie's challenge answers 401 and its forbid 403, with the status
/// code pages opted out so the problem-details middleware writes the body, the way the
/// <c>Api</c> scheme answers. A page request keeps the login and access-denied redirects the
/// Users module configures.
/// </summary>
public sealed class ApiCookieResponses : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (!string.Equals(name, IdentityConstants.ApplicationScheme, StringComparison.Ordinal))
        {
            return;
        }

        options.Events ??= new CookieAuthenticationEvents();
        var events = options.Events;
        var toLogin = events.OnRedirectToLogin;
        var toAccessDenied = events.OnRedirectToAccessDenied;
        events.OnRedirectToLogin = context => AccessGate.IsApiPath(context.Request.Path)
            ? AnswerAsync(context.HttpContext, StatusCodes.Status401Unauthorized)
            : toLogin(context);
        events.OnRedirectToAccessDenied = context => AccessGate.IsApiPath(context.Request.Path)
            ? AnswerAsync(context.HttpContext, StatusCodes.Status403Forbidden)
            : toAccessDenied(context);
    }

    private static Task AnswerAsync(HttpContext context, int statusCode)
    {
        if (context.Features.Get<IStatusCodePagesFeature>() is { } statusCodePages)
        {
            statusCodePages.Enabled = false;
        }

        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
