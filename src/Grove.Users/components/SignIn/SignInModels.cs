using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Grove.Users.Components;

// The sign-in API's wire shapes (api/grove/auth, Grove.Users' AuthApi; the login extensions of
// Grove.Access), field for field.

/// <summary>A sign-in form's shape: local sign-in, the remember-me policy and what the enabled features contribute.</summary>
public sealed record AccessForm(bool LocalLogin, bool AllowRememberMe, bool RememberMe, AccessFormExtension[] Extensions, string? Redirect)
{
    public static AccessForm Empty { get; } = new(true, true, false, [], null);
}

/// <summary>A link, a provider button or a section a feature adds to a sign-in form (Kind: Link, Provider, Section).</summary>
public sealed record AccessFormExtension(string Id, string Kind, string Text, string? Url, string? Component, string? Header, int Order);

/// <summary>Who signed in, or that the second step is pending, or where to go first.</summary>
public sealed record SignInAnswer(bool IsAuthenticated, string? UserName, string[]? Roles, bool RequiresTwoFactor, string? Redirect);

/// <summary>A refused sign-in: what to show, and the page the refusal names (a pending confirmation).</summary>
public sealed record SignInRefusal(string[]? Errors, string? Redirect);

/// <summary>The pending second step: the account's methods and the one to ask for first.</summary>
public sealed record TwoFactorChallengeState(string[] Providers, string? Provider, bool AllowRememberDevice);

public sealed record TwoFactorCodeSent(bool Sent, string? Message);

/// <summary>The sign-in forms the extensions name (Grove.Access' AccessForms).</summary>
public static class AccessForms
{
    public const string Login = "login";

    public const string Register = "register";

    public const string ForgotPassword = "forgot-password";

    public const string ResetPassword = "reset-password";
}

/// <summary>The two-factor methods by their Identity provider names, as people know them.</summary>
public static class TwoFactorMethods
{
    public const string Authenticator = "Authenticator";

    public const string Email = "Email";

    public const string Phone = "Phone";

    public static string DisplayName(string provider) => provider switch
    {
        Authenticator => "Authenticator app",
        Email => "Email",
        Phone => "SMS",
        _ => provider,
    };

    /// <summary>Whether the method sends its code (email, SMS) rather than showing it (an app).</summary>
    public static bool SendsCode(string? provider) => provider is Email or Phone;
}

/// <summary>
/// Calls to the sign-in and account APIs from any shell (relative to the document base; the shell
/// middleware strips a shell's base off <c>{base}/api/…</c>), with the headers a form's sections
/// contributed (a captcha's token). A refusal comes back as its body, never as an exception.
/// </summary>
internal static class SignInApi
{
    public static async Task<T?> GetAsync<T>(HttpClient http, string uri)
    {
        try
        {
            using var response = await http.GetAsync(uri);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : default;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
            return default;
        }
    }

    /// <summary>Posts <paramref name="body"/>; the success body, or the refusal body (400, 401), or neither when the call failed.</summary>
    public static async Task<(TResult? Result, TRefusal? Refusal)> PostAsync<TResult, TRefusal>(HttpClient http, string uri, object? body, IReadOnlyDictionary<string, string?>? headers = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body ?? new { }) };
        foreach (var (name, value) in headers ?? new Dictionary<string, string?>())
        {
            if (!string.IsNullOrEmpty(value))
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        try
        {
            using var response = await http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return (response.StatusCode == HttpStatusCode.NoContent ? default : await response.Content.ReadFromJsonAsync<TResult>(), default);
            }

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized && response.Content.Headers.ContentLength != 0)
            {
                return (default, await response.Content.ReadFromJsonAsync<TRefusal>());
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
        }

        return (default, default);
    }
}
