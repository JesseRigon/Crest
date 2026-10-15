using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Grove.Themes.Admin.Api;

/// <summary>One registered branding rendition and its current file (docs/branding.md).</summary>
public sealed record GroveBrandingRendition(string Name, string Label, string UsedBy, int? Width, int? Height, string? Accept, bool Reserved, string? Path, string Url);

/// <summary>The branding group: the display name (edited with the general settings) and the renditions.</summary>
public sealed record GroveBrandingSettings(string SiteName, GroveBrandingRendition[] Renditions);

public interface IBrandingApi
{
    Task<GroveBrandingSettings?> GetAsync();

    /// <summary>Uploads a rendition's file; the result, or the server's message when it refused.</summary>
    Task<(GroveBrandingSettings? Settings, string? Error)> UploadAsync(string rendition, string fileName, Stream content);

    Task<GroveBrandingSettings?> ClearAsync(string rendition);
}

public sealed class BrandingApi(HttpClient http) : IBrandingApi
{
    private const string Base = "api/grove/site/branding-settings";

    public async Task<GroveBrandingSettings?> GetAsync()
    {
        using var response = await http.SendAsync(C(new(HttpMethod.Get, Base)));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<GroveBrandingSettings>() : null;
    }

    public async Task<(GroveBrandingSettings? Settings, string? Error)> UploadAsync(string rendition, string fileName, Stream content)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(content), "file", fileName);
        using var request = C(new(HttpMethod.Post, $"{Base}/{Uri.EscapeDataString(rendition)}"));
        request.Content = form;
        using var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<GroveBrandingSettings>(), null)
            : (null, await response.Content.ReadAsStringAsync() is { Length: > 0 } message ? message : response.ReasonPhrase);
    }

    public async Task<GroveBrandingSettings?> ClearAsync(string rendition)
    {
        using var response = await http.SendAsync(C(new(HttpMethod.Delete, $"{Base}/{Uri.EscapeDataString(rendition)}")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<GroveBrandingSettings>() : null;
    }

    private static HttpRequestMessage C(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}
