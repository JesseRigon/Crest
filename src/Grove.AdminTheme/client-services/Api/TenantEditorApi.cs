using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Grove.AdminTheme.Api;

/// <summary>A tenant's editable settings (the create and edit forms).</summary>
public sealed class TenantForm
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public string? RequestUrlHost { get; set; }

    public string? RequestUrlPrefix { get; set; }

    public string? DatabaseProvider { get; set; }

    /// <summary>Shown with its password redacted; sending the redacted value back keeps the stored one.</summary>
    public string? ConnectionString { get; set; }

    public string? TablePrefix { get; set; }

    public string? Schema { get; set; }

    public string? RecipeName { get; set; }

    public string[] FeatureProfiles { get; set; } = [];
}

/// <summary>A choice the tenant editor offers.</summary>
public sealed record TenantChoice(string Value, string Text, bool HasConnectionString = false);

/// <summary>The tenant editor: the form and what of it may be changed.</summary>
public sealed record TenantEditor(
    TenantForm Form,
    bool IsNew,
    bool IsUninitialized,
    bool CanEditDatabase,
    bool DatabaseProviderPreset,
    bool DatabaseConfigurationPreset,
    bool RequireTablePrefix,
    bool HasTablePrefixPattern,
    bool HasSchemaPattern,
    string? ResolvedTablePrefix,
    string? ResolvedSchema,
    TenantChoice[] Recipes,
    TenantChoice[] FeatureProfiles,
    TenantChoice[] DatabaseProviders);

/// <summary>A refusal, keyed by the field it concerns ("" for the whole form).</summary>
public sealed record TenantFieldError(string Key, string Message);

/// <summary>The outcome of a create, edit or setup: the tenant, or the refusals.</summary>
public sealed record TenantSaveResult(bool Succeeded, TenantFieldError[] Errors, GroveTenant? Tenant = null, string? SetupUrl = null)
{
    public static TenantSaveResult Failed(string message) => new(false, [new TenantFieldError(string.Empty, message)]);
}

/// <summary>Setting up an uninitialized tenant in place.</summary>
public sealed class TenantSetupRequest
{
    public string? SiteName { get; set; }

    public string? UserName { get; set; }

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? RecipeName { get; set; }

    public string? SiteTimeZone { get; set; }

    public string? DatabaseProvider { get; set; }

    public string? ConnectionString { get; set; }

    public string? TablePrefix { get; set; }

    public string? Schema { get; set; }
}

/// <summary>Tenant create, edit and setup (<c>api/grove/tenants</c>).</summary>
public interface ITenantEditorApi
{
    /// <summary>The create form (<paramref name="name"/> null) or a tenant's edit form.</summary>
    Task<TenantEditor?> GetAsync(string? name);

    /// <summary>Creates an uninitialized tenant; with <paramref name="setupUrl"/> the result carries its setup link.</summary>
    Task<TenantSaveResult> CreateAsync(TenantForm form, bool setupUrl = false);

    Task<TenantSaveResult> EditAsync(string name, TenantForm form);

    /// <summary>Sets an uninitialized tenant up in place (site, first administrator, recipe).</summary>
    Task<TenantSaveResult> SetupAsync(string name, TenantSetupRequest request);

    /// <summary>The tenant's own setup page with a 24-hour token.</summary>
    Task<string?> GetSetupUrlAsync(string name);
}

public sealed class TenantEditorApi(HttpClient http) : ITenantEditorApi
{
    private const string Root = "api/grove/tenants";

    public async Task<TenantEditor?> GetAsync(string? name)
    {
        var uri = string.IsNullOrWhiteSpace(name) ? $"{Root}/editor" : $"{Root}/editor?name={Uri.EscapeDataString(name)}";
        using var response = await http.SendAsync(C(new(HttpMethod.Get, uri)));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<TenantEditor>() : null;
    }

    public Task<TenantSaveResult> CreateAsync(TenantForm form, bool setupUrl = false) =>
        SendAsync(new(HttpMethod.Post, setupUrl ? $"{Root}?setupUrl=true" : Root), form);

    public Task<TenantSaveResult> EditAsync(string name, TenantForm form) =>
        SendAsync(new(HttpMethod.Put, $"{Root}/{Uri.EscapeDataString(name)}"), form);

    public Task<TenantSaveResult> SetupAsync(string name, TenantSetupRequest request) =>
        SendAsync(new(HttpMethod.Post, $"{Root}/{Uri.EscapeDataString(name)}/setup"), request);

    public async Task<string?> GetSetupUrlAsync(string name)
    {
        using var response = await http.SendAsync(C(new(HttpMethod.Get, $"{Root}/{Uri.EscapeDataString(name)}/setup-url")));
        return response.IsSuccessStatusCode ? (await response.Content.ReadFromJsonAsync<TenantSetupUrl>())?.Url : null;
    }

    // A refusal comes back as the same result shape with Succeeded false (400); anything else
    // (403, a failure without a body) as one message.
    private async Task<TenantSaveResult> SendAsync(HttpRequestMessage request, object body)
    {
        using (request)
        {
            request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(C(request));
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                try
                {
                    return await response.Content.ReadFromJsonAsync<TenantSaveResult>() ?? TenantSaveResult.Failed("The server returned no result.");
                }
                catch (System.Text.Json.JsonException)
                {
                }
            }

            return TenantSaveResult.Failed($"The request failed ({(int)response.StatusCode}).");
        }
    }

    private sealed record TenantSetupUrl(string? Url);

    private static HttpRequestMessage C(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}
