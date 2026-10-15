using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Grove.Access;

/// <summary>
/// The sign-in forms a login extension can contribute to: the staff sign-in and the account
/// forms around it.
/// </summary>
public static class AccessForms
{
    public const string Login = "login";

    public const string Register = "register";

    public const string ForgotPassword = "forgot-password";

    public const string ResetPassword = "reset-password";
}

/// <summary>What a login extension contributes to a sign-in form.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LoginExtensionKind>))]
public enum LoginExtensionKind
{
    /// <summary>A link under the form (forgot your password, create an account).</summary>
    Link,

    /// <summary>A button that starts a sign-in with an external identity provider (a navigation).</summary>
    Provider,

    /// <summary>
    /// A component the form renders inside itself (a captcha): named in the shell's component
    /// registry; its value is sent with the form's submission under <see cref="LoginExtension.Header"/>.
    /// </summary>
    Section,
}

/// <summary>
/// One contribution to a sign-in form. <see cref="Url"/> is an absolute path on the request's
/// tenant (links and providers are navigations, never fetches).
/// </summary>
/// <param name="Id">Stable within the form; the test hook and the key the page renders by.</param>
/// <param name="Kind">Link, provider button or section.</param>
/// <param name="Text">The invariant caption (a provider's display name).</param>
/// <param name="Url">Where a link or a provider button goes.</param>
/// <param name="Component">A section's component, by its registry name.</param>
/// <param name="Header">The request header a section's value is sent under.</param>
/// <param name="Order">The position among the form's contributions of the same kind.</param>
public sealed record LoginExtension(
    string Id,
    LoginExtensionKind Kind,
    string Text,
    string? Url = null,
    string? Component = null,
    string? Header = null,
    int Order = 0);

/// <summary>The form being built and the request it is built for.</summary>
public sealed record LoginExtensionContext(string Form, HttpContext HttpContext, string? ReturnUrl)
{
    /// <summary>An absolute path on the request's tenant: the tenant base plus <paramref name="page"/>.</summary>
    public string TenantPath(string page) => $"{HttpContext.Request.PathBase}{page}";
}

/// <summary>
/// Contributes to the sign-in forms (<see cref="AccessForms"/>): the links, provider buttons and
/// sections a feature adds, in place of display drivers on the form. Every enabled feature's
/// providers are asked; the form renders what they answer and nothing is listed by hand.
/// </summary>
public interface ILoginExtensionProvider
{
    Task<IEnumerable<LoginExtension>> GetExtensionsAsync(LoginExtensionContext context);
}
