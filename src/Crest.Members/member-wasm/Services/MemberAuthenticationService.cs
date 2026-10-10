using System.Net.Http;
using System.Net.Http.Json;
using Crest.MemberTheme.Services;
using Crest.Members.Models;

namespace Crest.Members.Member.Services;

/// <summary>
/// Member sign-in, registration and sign-out against this module's portal APIs.
/// </summary>
/// <remarks>
/// The downstream half of Crest's <see cref="IMemberAuthenticationService"/> seam. Crest
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
    private const string PortalApi = "api/crest/members/portal";

    public async Task<MemberAuthenticationResult> SignInAsync(string userName, string password, bool rememberMe)
    {
        using var response = await http.PostAsJsonAsync(
            $"{PortalApi}/login",
            new PortalLoginRequest(userName, password, rememberMe, OrganizationId: null));

        return response.IsSuccessStatusCode
            ? MemberAuthenticationResult.Success
            : MemberAuthenticationResult.Failed("That user name and password do not match an account.");
    }

    public async Task<MemberAuthenticationResult> RegisterAsync(string userName, string email, string password, string? organizationId)
    {
        using var response = await http.PostAsJsonAsync(
            $"{PortalApi}/register",
            new { userName, email, password, organizationId });

        if (response.IsSuccessStatusCode)
        {
            return MemberAuthenticationResult.Success;
        }

        // Registration CAN say more than sign-in: the caller is creating an account, so
        // "that name is taken" is information they need and have not had to guess at.
        var detail = await response.Content.ReadAsStringAsync();
        return MemberAuthenticationResult.Failed(
            string.IsNullOrWhiteSpace(detail)
                ? "Could not create the account."
                : detail);
    }

    public async Task SignOutAsync()
    {
        using var response = await http.PostAsync("api/crest/auth/logout", null);
    }
}
