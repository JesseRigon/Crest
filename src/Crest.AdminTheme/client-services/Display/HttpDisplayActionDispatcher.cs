using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;

namespace Crest.AdminTheme.Display;

/// <summary>
/// The shells' side of a display tree's server actions and data slots (docs/blazor-display.md
/// § 4a): every action and every slot read is a request through the shell's HttpClient (cookie,
/// antiforgery, the X-Shell side), so the server's gate and decision see it like any other
/// request, in a Server circuit as in the browser. A workflow runs through
/// <c>api/crest/workflows/actions</c>, a slot through <c>api/queries</c>; a submit goes to the
/// page that owns the tree (<see cref="SubmitHandler"/>), since only it knows the item.
/// </summary>
public sealed class HttpDisplayActionDispatcher(HttpClient http, NavigationManager navigation) : IDisplayActionDispatcher, IDisplayDataSource
{
    /// <summary>Set by the page that renders an editable tree; a submit without one is refused.</summary>
    public Func<string, JsonNode?, CancellationToken, Task>? SubmitHandler { get; set; }

    public async Task<JsonNode?> RunWorkflowAsync(string workflow, IReadOnlyDictionary<string, JsonNode?> input, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject();
        foreach (var (name, value) in input)
        {
            body[name] = value?.DeepClone();
        }

        using var response = await http.PostAsJsonAsync($"api/crest/workflows/actions/{Uri.EscapeDataString(workflow)}", body, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = result?["error"]?.GetValue<string>() ?? $"The workflow action answered {(int)response.StatusCode}.";

            // Refused arguments come back by input name, for the form that sent them.
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && result?["errors"] is JsonObject errors)
            {
                throw new DisplayActionException(message, errors.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<string>)(pair.Value as JsonArray ?? []).Select(error => error?.GetValue<string>() ?? string.Empty).ToList()));
            }

            throw new InvalidOperationException(message);
        }

        return result?["output"]?.DeepClone();
    }

    public void Navigate(string url)
    {
        // Same-site navigation only: an absolute URL to another origin is not followed.
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && !navigation.BaseUri.StartsWith(absolute.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Navigation to '{absolute.GetLeftPart(UriPartial.Authority)}' is not allowed from a display action.");
        }

        navigation.NavigateTo(url);
    }

    public Task SubmitAsync(string nodeId, JsonNode? values, CancellationToken cancellationToken = default) =>
        SubmitHandler is { } handler
            ? handler(nodeId, values, cancellationToken)
            : throw new InvalidOperationException("Nothing on this page accepts a submit.");

    public async Task<JsonNode?> QueryAsync(string query, IReadOnlyDictionary<string, JsonNode?> parameters, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject();
        foreach (var (name, value) in parameters)
        {
            body[name] = value?.DeepClone();
        }

        using var response = await http.PostAsJsonAsync($"api/queries/{Uri.EscapeDataString(query)}", body, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken) : null;
    }
}
