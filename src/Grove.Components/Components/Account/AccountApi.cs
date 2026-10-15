using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Grove.Components.Account;

/// <summary>
/// The account API from any shell (relative to the document base; the shell middleware strips a
/// shell's base off <c>{base}/api/…</c>). A refusal (400) carries the same result shape as a
/// success.
/// </summary>
internal static class AccountApi
{
    public const string Root = "api/grove/account";

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

    public static async Task<TResult?> SendAsync<TResult>(HttpClient http, HttpMethod method, string uri, object? body)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        try
        {
            using var response = await http.SendAsync(request);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest)
            {
                return await response.Content.ReadFromJsonAsync<TResult>();
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
        }

        return default;
    }
}
