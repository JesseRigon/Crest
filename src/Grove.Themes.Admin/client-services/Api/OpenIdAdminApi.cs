using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Grove.Themes.Admin.Api;

// The OpenID Connect administration (api/grove/openid, Grove.Access.OpenId's OpenIdAdminApi): the
// wire shapes match the server's records field for field.

public sealed record OpenIdApplicationListItem(string Id, string? ClientId, string? DisplayName, string? Type);

/// <summary>An application in the editor. The client secret is write-only: never read back,
/// <see cref="HasClientSecret"/> says whether one is stored, an empty value keeps it.</summary>
public sealed class OpenIdApplicationModel
{
    public string? Id { get; set; }
    public string? ClientId { get; set; }
    public string? DisplayName { get; set; }
    public string? Type { get; set; } = "confidential";
    public string? ConsentType { get; set; } = "explicit";
    public string? ClientSecret { get; set; }
    public bool HasClientSecret { get; set; }
    public string? RedirectUris { get; set; }
    public string? PostLogoutRedirectUris { get; set; }
    public bool AllowAuthorizationCodeFlow { get; set; }
    public bool AllowClientCredentialsFlow { get; set; }
    public bool AllowHybridFlow { get; set; }
    public bool AllowImplicitFlow { get; set; }
    public bool AllowPasswordFlow { get; set; }
    public bool AllowRefreshTokenFlow { get; set; }
    public bool AllowLogoutEndpoint { get; set; }
    public bool AllowIntrospectionEndpoint { get; set; }
    public bool AllowRevocationEndpoint { get; set; }
    public bool RequireProofKeyForCodeExchange { get; set; }
    public bool RequirePushedAuthorizationRequests { get; set; }
    public string[] Roles { get; set; } = [];
    public string[] Scopes { get; set; } = [];
}

public sealed record OpenIdServerFlows(bool AuthorizationCode, bool ClientCredentials, bool Hybrid, bool Implicit, bool Password, bool RefreshToken, bool Logout, bool Introspection, bool Revocation);

public sealed record OpenIdApplicationEditor(OpenIdApplicationModel? Application, string[] Roles, string[] Scopes, OpenIdServerFlows? ServerFlows);

public sealed record OpenIdFieldError(string Key, string Message);

public sealed record OpenIdSaveResult(bool Succeeded, OpenIdFieldError[] Errors, string? Id = null)
{
    public static OpenIdSaveResult Failed(string message) => new(false, [new OpenIdFieldError(string.Empty, message)]);
}

public sealed class OpenIdScopeModel
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }

    /// <summary>Space-separated resources (audiences) the scope grants.</summary>
    public string? Resources { get; set; }
}

public sealed record OpenIdCertificate(string StoreLocation, string StoreName, string? FriendlyName, string Subject, string Issuer, DateTime NotBefore, DateTime NotAfter, string Thumbprint, bool HasPrivateKey);

/// <summary>The server configuration (structured; endpoints switch on and off at fixed paths).</summary>
public sealed class OpenIdServerConfiguration
{
    public string AccessTokenFormat { get; set; } = "JsonWebToken";
    public string? Authority { get; set; }
    public bool DisableAccessTokenEncryption { get; set; }
    public string? EncryptionCertificateStoreLocation { get; set; }
    public string? EncryptionCertificateStoreName { get; set; }
    public string? EncryptionCertificateThumbprint { get; set; }
    public string? SigningCertificateStoreLocation { get; set; }
    public string? SigningCertificateStoreName { get; set; }
    public string? SigningCertificateThumbprint { get; set; }
    public bool EnableAuthorizationEndpoint { get; set; }
    public bool EnableLogoutEndpoint { get; set; }
    public bool EnableTokenEndpoint { get; set; }
    public bool EnableUserInfoEndpoint { get; set; }
    public bool EnableIntrospectionEndpoint { get; set; }
    public bool EnablePushedAuthorizationEndpoint { get; set; }
    public bool EnableRevocationEndpoint { get; set; }
    public bool AllowAuthorizationCodeFlow { get; set; }
    public bool AllowClientCredentialsFlow { get; set; }
    public bool AllowHybridFlow { get; set; }
    public bool AllowImplicitFlow { get; set; }
    public bool AllowPasswordFlow { get; set; }
    public bool AllowRefreshTokenFlow { get; set; }
    public bool DisableRollingRefreshTokens { get; set; }
    public bool UseReferenceAccessTokens { get; set; }
    public bool RequireProofKeyForCodeExchange { get; set; }
    public bool RequirePushedAuthorizationRequests { get; set; }
    public bool RequireEndSessionConfirmation { get; set; } = true;
    public OpenIdCertificate[] AvailableCertificates { get; set; } = [];
}

public sealed class OpenIdValidationConfiguration
{
    public string? Authority { get; set; }
    public string? MetadataAddress { get; set; }
    public string? Audience { get; set; }
    public bool DisableTokenTypeValidation { get; set; }
    public string? Tenant { get; set; }
}

public interface IOpenIdAdminApi
{
    Task<OpenIdApplicationListItem[]?> ListApplicationsAsync();
    Task<OpenIdApplicationEditor?> GetApplicationEditorAsync(string? id);
    Task<OpenIdSaveResult> SaveApplicationAsync(OpenIdApplicationModel model);
    Task<bool> DeleteApplicationAsync(string id);
    Task<OpenIdScopeModel[]?> ListScopesAsync();
    Task<OpenIdScopeModel?> GetScopeAsync(string id);
    Task<OpenIdSaveResult> SaveScopeAsync(OpenIdScopeModel model);
    Task<bool> DeleteScopeAsync(string id);
    Task<OpenIdServerConfiguration?> GetServerAsync();
    Task<OpenIdSaveResult> SaveServerAsync(OpenIdServerConfiguration model);
    Task<OpenIdValidationConfiguration?> GetValidationAsync();
    Task<OpenIdSaveResult> SaveValidationAsync(OpenIdValidationConfiguration model);
}

public sealed class OpenIdAdminApi(HttpClient http) : IOpenIdAdminApi
{
    private const string Root = "api/grove/openid";

    public Task<OpenIdApplicationListItem[]?> ListApplicationsAsync() => GetAsync<OpenIdApplicationListItem[]>($"{Root}/applications");

    public Task<OpenIdApplicationEditor?> GetApplicationEditorAsync(string? id) =>
        GetAsync<OpenIdApplicationEditor>(string.IsNullOrEmpty(id) ? $"{Root}/applications/editor" : $"{Root}/applications/editor?id={Uri.EscapeDataString(id)}");

    public Task<OpenIdSaveResult> SaveApplicationAsync(OpenIdApplicationModel model) => string.IsNullOrEmpty(model.Id)
        ? SendAsync(new(HttpMethod.Post, $"{Root}/applications"), model)
        : SendAsync(new(HttpMethod.Put, $"{Root}/applications/{Uri.EscapeDataString(model.Id)}"), model);

    public Task<bool> DeleteApplicationAsync(string id) => DeleteAsync($"{Root}/applications/{Uri.EscapeDataString(id)}");

    public Task<OpenIdScopeModel[]?> ListScopesAsync() => GetAsync<OpenIdScopeModel[]>($"{Root}/scopes");

    public Task<OpenIdScopeModel?> GetScopeAsync(string id) => GetAsync<OpenIdScopeModel>($"{Root}/scopes/{Uri.EscapeDataString(id)}");

    public Task<OpenIdSaveResult> SaveScopeAsync(OpenIdScopeModel model) => string.IsNullOrEmpty(model.Id)
        ? SendAsync(new(HttpMethod.Post, $"{Root}/scopes"), model)
        : SendAsync(new(HttpMethod.Put, $"{Root}/scopes/{Uri.EscapeDataString(model.Id)}"), model);

    public Task<bool> DeleteScopeAsync(string id) => DeleteAsync($"{Root}/scopes/{Uri.EscapeDataString(id)}");

    public Task<OpenIdServerConfiguration?> GetServerAsync() => GetAsync<OpenIdServerConfiguration>($"{Root}/server");

    public Task<OpenIdSaveResult> SaveServerAsync(OpenIdServerConfiguration model) => SendAsync(new(HttpMethod.Put, $"{Root}/server"), model);

    public Task<OpenIdValidationConfiguration?> GetValidationAsync() => GetAsync<OpenIdValidationConfiguration>($"{Root}/validation");

    public Task<OpenIdSaveResult> SaveValidationAsync(OpenIdValidationConfiguration model) => SendAsync(new(HttpMethod.Put, $"{Root}/validation"), model);

    private async Task<T?> GetAsync<T>(string uri)
    {
        using var response = await http.SendAsync(C(new(HttpMethod.Get, uri)));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : default;
    }

    private async Task<bool> DeleteAsync(string uri)
    {
        using var response = await http.SendAsync(C(new(HttpMethod.Delete, uri)));
        return response.IsSuccessStatusCode;
    }

    private async Task<OpenIdSaveResult> SendAsync(HttpRequestMessage request, object body)
    {
        using (request)
        {
            request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(C(request));
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                try
                {
                    return await response.Content.ReadFromJsonAsync<OpenIdSaveResult>() ?? OpenIdSaveResult.Failed("The server returned no result.");
                }
                catch (System.Text.Json.JsonException)
                {
                }
            }

            return OpenIdSaveResult.Failed($"The request failed ({(int)response.StatusCode}).");
        }
    }

    private static HttpRequestMessage C(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}
