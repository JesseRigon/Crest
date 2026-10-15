using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Grove.AdminTheme.Api;

/// <summary>A stored addressing map, as the editor lists it (docs/regions.md › Addressing maps as runtime rules).</summary>
public sealed record GroveAddressingMapSummary(string Country, bool Edited, int LevelCount, string PostalCodeLabel, string? PostalCodePattern);

public sealed record GroveAddressingMapWriteResult(bool Succeeded, string[] Errors);

/// <summary>The addressing maps (global reference data the Default tenant edits). A map travels as JSON: the shape is Grove.Regions'.</summary>
public interface IRegionsApi
{
    Task<GroveAddressingMapSummary[]?> ListAddressingMapsAsync();
    Task<JsonObject?> GetAddressingMapAsync(string country);
    Task<GroveAddressingMapWriteResult> SaveAddressingMapAsync(string country, JsonObject map);
    Task<GroveAddressingMapWriteResult> RevertAddressingMapAsync(string country);
}

public sealed class RegionsApi(HttpClient http) : IRegionsApi
{
    private const string Base = "api/grove/regions/addressing-maps";

    public async Task<GroveAddressingMapSummary[]?> ListAddressingMapsAsync()
    {
        using var response = await http.SendAsync(C(new(HttpMethod.Get, Base)));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<GroveAddressingMapSummary[]>() : null;
    }

    public async Task<JsonObject?> GetAddressingMapAsync(string country)
    {
        using var response = await http.SendAsync(C(new(HttpMethod.Get, $"{Base}/{Uri.EscapeDataString(country)}")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<JsonObject>() : null;
    }

    public async Task<GroveAddressingMapWriteResult> SaveAddressingMapAsync(string country, JsonObject map)
    {
        using var request = C(new(HttpMethod.Put, $"{Base}/{Uri.EscapeDataString(country)}"));
        request.Content = JsonContent.Create(map);
        using var response = await http.SendAsync(request);
        return await ResultAsync(response);
    }

    public async Task<GroveAddressingMapWriteResult> RevertAddressingMapAsync(string country)
    {
        using var response = await http.SendAsync(C(new(HttpMethod.Delete, $"{Base}/{Uri.EscapeDataString(country)}")));
        return await ResultAsync(response);
    }

    private static async Task<GroveAddressingMapWriteResult> ResultAsync(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            try
            {
                if (await response.Content.ReadFromJsonAsync<GroveAddressingMapWriteResult>() is { Errors: not null } result) return result;
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }

        return new(response.IsSuccessStatusCode, response.IsSuccessStatusCode ? [] : [$"{(int)response.StatusCode} {response.ReasonPhrase}"]);
    }

    private static HttpRequestMessage C(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}
