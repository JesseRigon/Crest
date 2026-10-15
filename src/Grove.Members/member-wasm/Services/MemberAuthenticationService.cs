using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Grove.MemberTheme.Services;
using Grove.Members.Models;

namespace Grove.Members.Member.Services;

/// <summary>
/// Member sign-in, registration and sign-out against this module's portal APIs.
/// </summary>
/// <remarks>
/// The downstream half of Grove's <see cref="IMemberAuthenticationService"/> seam. Grove
/// owns the login SURFACE - a page in the member shell, at the member base, rendered
/// InteractiveWebAssembly so the auth cookie reaches the browser - and this module owns
/// what signing in MEANS: the member class, the organization binding, provisioning.
///
/// <para>
/// Failures are reported as one opaque message, deliberately. The portal APIs already
/// avoid telling an unauthenticated caller whether a user name exists, and this must not
/// reintroduce that: "wrong user name" and "wrong password" are indistinguishable here on
/// purpose.
/// </para>
/// </remarks>
public sealed class MemberAuthenticationService(HttpClient http) : IMemberAuthenticationService
{
    private const string PortalApi = "api/grove/members/portal";

    public async Task<MemberAuthenticationResult> SignInAsync(string userName, string password, bool rememberMe)
    {
        using var response = await http.PostAsJsonAsync(
            $"{PortalApi}/login",
            new PortalLoginRequest(userName, password, rememberMe, OrganizationId: null));

        return response.IsSuccessStatusCode
            ? MemberAuthenticationResult.Success
            : MemberAuthenticationResult.Failed("That user name and password do not match an account.");
    }

    public async Task<MemberAuthenticationResult> RegisterAsync(string userName, string email, string password, string firstName, string lastName, string? organizationId)
    {
        using var response = await http.PostAsJsonAsync(
            $"{PortalApi}/register",
            new PortalRegisterRequest(userName, email, password, firstName, lastName, organizationId));

        if (response.IsSuccessStatusCode)
        {
            return MemberAuthenticationResult.Success;
        }

        // Registration CAN say more than sign-in: the caller is creating an account, so
        // "that name is taken" is information they need and have not had to guess at.
        return MemberAuthenticationResult.Failed(
            RefusalMessage(await response.Content.ReadAsStringAsync()) ?? "Could not create the account.");
    }

    // The refusal's messages, not its raw body: a PortalRefusal ("errors": [...]) or the
    // model validation problem details ("errors": { field: [...] }).
    internal static string? RefusalMessage(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("errors", out var errors))
            {
                return null;
            }

            var messages = errors.ValueKind switch
            {
                JsonValueKind.Array => errors.EnumerateArray().Select(error => error.GetString()),
                JsonValueKind.Object => errors.EnumerateObject()
                    .SelectMany(field => field.Value.ValueKind == JsonValueKind.Array
                        ? field.Value.EnumerateArray().Select(error => error.GetString())
                        : []),
                _ => [],
            };

            var text = string.Join(" ", messages.Where(message => !string.IsNullOrWhiteSpace(message)));
            return text.Length > 0 ? text : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SignOutAsync()
    {
        using var response = await http.PostAsync("api/grove/auth/logout", null);
    }

    public async Task<IReadOnlyList<MemberExternalProvider>> GetExternalProvidersAsync() =>
        [.. (await http.GetFromJsonAsync<PortalExternalProvider[]>($"{PortalApi}/external-providers") ?? [])
            .Select(provider => new MemberExternalProvider(provider.Name, provider.DisplayName))];

    public string ExternalLoginUrl(string provider, string? organizationId, string returnUrl)
    {
        var query = $"provider={Uri.EscapeDataString(provider)}&returnUrl={Uri.EscapeDataString(returnUrl)}";
        if (!string.IsNullOrWhiteSpace(organizationId))
        {
            query += $"&organizationId={Uri.EscapeDataString(organizationId)}";
        }

        return $"{PortalApi}/external-login?{query}";
    }

    // The same answer whether or not the account exists: an unauthenticated caller does not get
    // to learn that. Only "not available here" (the tenant turned password reset off) differs.
    public async Task<MemberAuthenticationResult> RequestPasswordResetAsync(string userNameOrEmail)
    {
        using var response = await http.PostAsJsonAsync($"{PortalApi}/forgot-password", new PortalForgotPasswordRequest(userNameOrEmail));
        return response.IsSuccessStatusCode
            ? MemberAuthenticationResult.Success
            : MemberAuthenticationResult.Failed("Password reset is not available on this site.");
    }

    public async Task<MemberAuthenticationResult> ResetPasswordAsync(string userNameOrEmail, string code, string newPassword)
    {
        using var response = await http.PostAsJsonAsync($"{PortalApi}/reset-password", new PortalResetPasswordRequest(userNameOrEmail, code, newPassword));
        if (response.IsSuccessStatusCode)
        {
            return MemberAuthenticationResult.Success;
        }

        return MemberAuthenticationResult.Failed(
            RefusalMessage(await response.Content.ReadAsStringAsync()) ?? "The password could not be reset.");
    }
}
