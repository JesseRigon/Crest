using System.Net.Http.Json;

namespace Grove.Sitemaps.BlazorWasm;

/// <summary>The API's error contract as the pages read it: a detail and the errors by field.</summary>
public sealed record ApiProblem(string? Title, string? Detail, Dictionary<string, string[]>? Errors)
{
    public static async Task<ApiProblem> ReadAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiProblem>() ?? new(null, response.ReasonPhrase, null);
        }
        catch
        {
            return new(null, response.ReasonPhrase, null);
        }
    }

    /// <summary>Every message, the field errors included, on one line.</summary>
    public string Message => Errors is { Count: > 0 } errors
        ? string.Join(" ", errors.SelectMany(error => error.Value))
        : Detail ?? Title ?? "The request failed.";
}
