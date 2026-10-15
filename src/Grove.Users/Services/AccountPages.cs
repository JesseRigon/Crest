#nullable enable

using Microsoft.AspNetCore.Http;

namespace Grove.Users.Services;

/// <summary>
/// The account pages, at the paths the account screens have always had, so the links already in
/// people's inboxes keep working. They are site-shell pages (Grove.Users' site library declares
/// the same literals); the server composes links to them here, in one place.
/// </summary>
public static class AccountPages
{
    public const string ConfirmEmail = "/ConfirmEmail";

    public const string ConfirmEmailSent = "/ConfirmEmailSent";

    public const string ChangePassword = "/ChangePassword";

    public const string ChangeEmail = "/ChangeEmail";

    public const string ExternalLogins = "/ExternalLogins";

    public const string Register = "/Register";

    public const string RegistrationPending = "/RegistrationPending";

    public const string ForgotPassword = "/ForgotPassword";

    public const string ForgotPasswordConfirmation = "/ForgotPasswordConfirmation";

    public const string ResetPassword = "/ResetPassword";

    public const string ResetPasswordConfirmation = "/ResetPasswordConfirmation";

    /// <summary>The signed-in user's two-factor page.</summary>
    public const string TwoFactor = "/TwoFactor";

    /// <summary>The second sign-in step, when a sign-in that did not start on the login page needs it (an external login).</summary>
    public const string LoginWithTwoFactor = "/LoginWithTwoFactorAuthentication";

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
