using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Crest.AdminTheme.Api;

/// <summary>One registered branding rendition and its current file (docs/branding.md).</summary>
public sealed record CrestBrandingRendition(string Name, string Label, string UsedBy, int? Width, int? Height, string? Accept, bool Reserved, string? Path, string Url);

/// <summary>The branding group: the display name (edited with the general settings) and the renditions.</summary>
public sealed record CrestBrandingSettings(string SiteName, CrestBrandingRendition[] Renditions);

public interface IBrandingApi
{
    Task<CrestBrandingSettings?> GetAsync();

    /// <summary>Uploads a rendition's file; the result, or the server's message when it refused.</summary>
    Task<(CrestBrandingSettings? Settings, string? Error)> UploadAsync(string rendition, string fileName, Stream content);

    Task<CrestBrandingSettings?> ClearAsync(string rendition);
}

public sealed class BrandingApi(HttpClient http) : IBrandingApi
{
    private const string Base = "api/crest/site/branding-settings";

    public async Task<CrestBrandingSettings?> GetAsync()
    {
        using var response = await http.SendAsync(C(new(HttpMethod.Get, Base)));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestBrandingSettings>() : null;
    }

    public async Task<(CrestBrandingSettings? Settings, string? Error)> UploadAsync(string rendition, string fileName, Stream content)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(content), "file", fileName);
        using var request = C(new(HttpMethod.Post, $"{Base}/{Uri.EscapeDataString(rendition)}"));
        request.Content = form;
        using var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<CrestBrandingSettings>(), null)
            : (null, await response.Content.ReadAsStringAsync() is { Length: > 0 } message ? message : response.ReasonPhrase);
    }

    public async Task<CrestBrandingSettings?> ClearAsync(string rendition)
    {
        using var response = await http.SendAsync(C(new(HttpMethod.Delete, $"{Base}/{Uri.EscapeDataString(rendition)}")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestBrandingSettings>() : null;
    }

    private static HttpRequestMessage C(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}
