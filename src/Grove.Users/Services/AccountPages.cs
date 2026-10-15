#nullable enable

using Microsoft.AspNetCore.Http;

namespace Grove.Users.Services;

/// <summary>
/// The account pages the stock MVC account screens were replaced by (step 7), at the paths those
/// screens had, so the links already in people's inboxes keep working. They are site-shell pages
/// (Grove.SiteTheme.Client's account pages declare the same literals); the server composes links
/// to them here, in one place.
/// </summary>
public static class AccountPages
{
    public const string ConfirmEmail = "/ConfirmEmail";

    public const string ConfirmEmailSent = "/ConfirmEmailSent";

    public const string ChangePassword = "/ChangePassword";

    public const string ChangeEmail = "/ChangeEmail";

    public const string ExternalLogins = "/ExternalLogins";

    /// <summary>An absolute URL to an account page on the request's tenant.</summary>
    public static string Url(HttpRequest request, string page, params (string Name, string? Value)[] query)
    {
        var url = $"{request.Scheme}://{request.Host}{request.PathBase}{page}";
        var parts = query.Where(part => part.Value is not null).Select(part => $"{part.Name}={Uri.EscapeDataString(part.Value!)}").ToArray();
        return parts.Length == 0 ? url : url + "?" + string.Join('&', parts);
    }

    /// <summary>The email confirmation link for a user and token.</summary>
    public static string ConfirmEmailUrl(HttpRequest request, string userId, string code) =>
        Url(request, ConfirmEmail, ("userId", userId), ("code", code));
}
