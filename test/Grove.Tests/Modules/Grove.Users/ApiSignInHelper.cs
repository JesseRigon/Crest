using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Grove.Tests.Modules.Grove.Users;

/// <summary>
/// Signs in through the sign-in API (<c>api/grove/auth/login</c>), as the login pages do: the
/// antiforgery token first (<c>api/grove/antiforgery/token</c>), then the credentials with it.
/// </summary>
public static class ApiSignInHelper
{
    public static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string userName, string password, CancellationToken cancellationToken = default)
    {
        using var tokenResponse = await client.GetAsync("api/grove/antiforgery/token", cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Post, "api/grove/auth/login")
        {
            Content = JsonContent.Create(new { userName, password, rememberMe = false }),
        };
        request.Headers.TryAddWithoutValidation(token!["headerName"]!.GetValue<string>(), token["requestToken"]!.GetValue<string>());
        CookiesHelper.CopyCookies(request, tokenResponse);

        return await client.SendAsync(request, cancellationToken);
    }
}
