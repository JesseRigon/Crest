using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Grove.Data.Connectors;
using Microsoft.AspNetCore.WebUtilities;

namespace Grove.Data.Protocols.OAuth;

/// <summary>
/// The OAuth 2.0 client flows (RFC 6749, 7636 PKCE, 8628 device code, 7009 revocation),
/// stateless: every request goes through the client the caller passes (the engine's guarded
/// client). Client authentication is <c>client_secret_post</c>, or HTTP Basic when the
/// connection's auth settings say <c>tokenEndpointAuth: client_secret_basic</c>.
/// </summary>
public sealed class OAuthClient : IOAuthClient
{
    private const int MaxResponseBytes = 256 * 1024;

    /// <summary>The auth setting naming the token endpoint's client authentication.</summary>
    public const string TokenEndpointAuthSetting = "tokenEndpointAuth";
    public const string ClientSecretBasic = "client_secret_basic";
    public const string ClientSecretPost = "client_secret_post";

    /// <summary>Whether the connection authenticates its client with HTTP Basic at the token, refresh and revoke endpoints.</summary>
    public static bool UsesBasic(ConnectionDefinition connection) =>
        connection.Auth.Settings.TryGetValue(TokenEndpointAuthSetting, out var value) && string.Equals(value?.Trim(), ClientSecretBasic, StringComparison.OrdinalIgnoreCase);

    /// <summary>Adds the client's authentication to a token-endpoint form (post) or returns the Basic header value (basic).</summary>
    private static string? Authenticate(ConnectionDefinition connection, AppCredentials app, Dictionary<string, string> form)
    {
        if (UsesBasic(connection) && !string.IsNullOrEmpty(app.ClientSecret))
        {
            form.Remove("client_id");
            form.Remove("client_secret");
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Uri.EscapeDataString(app.ClientId)}:{Uri.EscapeDataString(app.ClientSecret)}"));
        }

        form["client_id"] = app.ClientId;
        if (!string.IsNullOrEmpty(app.ClientSecret))
        {
            form["client_secret"] = app.ClientSecret;
        }

        return null;
    }

    public Uri BuildAuthorizeUrl(ConnectionDefinition connection, AppCredentials app, string redirectUri, string state, string? codeVerifier)
    {
        var authorizeUrl = connection.Auth.AuthorizeUrl ?? throw new InvalidOperationException($"Connection '{connection.Key}' has no authorization endpoint.");
        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = app.ClientId,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
        };
        if (connection.Auth.Scopes.Count > 0)
        {
            query["scope"] = string.Join(' ', connection.Auth.Scopes);
        }

        if (connection.Auth.UsePkce && codeVerifier is not null)
        {
            query["code_challenge"] = CodeChallenge(codeVerifier);
            query["code_challenge_method"] = "S256";
        }

        foreach (var (name, value) in connection.Auth.Settings)
        {
            if (name.StartsWith("authorize:", StringComparison.OrdinalIgnoreCase))
            {
                query[name["authorize:".Length..]] = value;
            }
        }

        return new Uri(QueryHelpers.AddQueryString(authorizeUrl, query));
    }

    public Task<OAuthTokenResult> ExchangeCodeAsync(HttpClient http, ConnectionDefinition connection, AppCredentials app, string code, string redirectUri, string? codeVerifier, CancellationToken cancellationToken = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
        };
        if (codeVerifier is not null)
        {
            form["code_verifier"] = codeVerifier;
        }

        return TokenAsync(http, connection, app, form, null, cancellationToken);
    }

    public Task<OAuthTokenResult> RefreshAsync(HttpClient http, ConnectionDefinition connection, AppCredentials app, string refreshToken, CancellationToken cancellationToken = default) =>
        TokenAsync(http, connection, app, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        }, refreshToken, cancellationToken);

    public Task<OAuthTokenResult> RequestClientCredentialsAsync(HttpClient http, ConnectionDefinition connection, AppCredentials app, CancellationToken cancellationToken = default)
    {
        var form = new Dictionary<string, string> { ["grant_type"] = "client_credentials" };
        if (connection.Auth.Scopes.Count > 0)
        {
            form["scope"] = string.Join(' ', connection.Auth.Scopes);
        }

        return TokenAsync(http, connection, app, form, null, cancellationToken);
    }

    public async Task<DeviceAuthorization?> StartDeviceAuthorizationAsync(HttpClient http, ConnectionDefinition connection, AppCredentials app, CancellationToken cancellationToken = default)
    {
        if (connection.Auth.DeviceAuthorizationUrl is not { } url)
        {
            return null;
        }

        var form = new Dictionary<string, string> { ["client_id"] = app.ClientId };
        if (connection.Auth.Scopes.Count > 0)
        {
            form["scope"] = string.Join(' ', connection.Auth.Scopes);
        }

        var (ok, json) = await PostAsync(http, url, form, cancellationToken);
        if (!ok || json is not { } root)
        {
            return null;
        }

        var deviceCode = String(root, "device_code");
        var userCode = String(root, "user_code");
        var verification = String(root, "verification_uri") ?? String(root, "verification_url");
        if (deviceCode is null || userCode is null || verification is null)
        {
            return null;
        }

        var expiresIn = Int(root, "expires_in") ?? 600;
        var interval = Int(root, "interval") ?? 5;
        return new DeviceAuthorization(deviceCode, userCode, verification, String(root, "verification_uri_complete"), DateTime.UtcNow.AddSeconds(expiresIn), TimeSpan.FromSeconds(interval));
    }

    public Task<OAuthTokenResult> PollDeviceTokenAsync(HttpClient http, ConnectionDefinition connection, AppCredentials app, string deviceCode, CancellationToken cancellationToken = default) =>
        TokenAsync(http, connection, app, new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            ["device_code"] = deviceCode,
        }, null, cancellationToken);

    public async Task<bool> RevokeAsync(HttpClient http, ConnectionDefinition connection, AppCredentials app, string token, bool isRefreshToken, CancellationToken cancellationToken = default)
    {
        if (connection.Auth.RevokeUrl is not { } url)
        {
            return false;
        }

        var form = new Dictionary<string, string>
        {
            ["token"] = token,
            ["token_type_hint"] = isRefreshToken ? "refresh_token" : "access_token",
        };
        var basic = Authenticate(connection, app, form);
        var (ok, _) = await PostAsync(http, url, form, cancellationToken, basic);
        return ok;
    }

    public string CreateCodeVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>The S256 challenge of a verifier (RFC 7636 4.2).</summary>
    public static string CodeChallenge(string codeVerifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    private static async Task<OAuthTokenResult> TokenAsync(HttpClient http, ConnectionDefinition connection, AppCredentials app, Dictionary<string, string> form, string? keepRefreshToken, CancellationToken cancellationToken)
    {
        if (connection.Auth.TokenUrl is not { } tokenUrl)
        {
            return new OAuthTokenResult(null, "invalid_request", $"Connection '{connection.Key}' has no token endpoint.");
        }

        var basic = Authenticate(connection, app, form);
        var (ok, json) = await PostAsync(http, tokenUrl, form, cancellationToken, basic);
        if (json is not { } root)
        {
            return new OAuthTokenResult(null, "invalid_response", "The token endpoint returned no JSON.");
        }

        if (!ok || String(root, "error") is { } errorCode && String(root, "access_token") is null)
        {
            var code = String(root, "error") ?? "invalid_response";
            return new OAuthTokenResult(null, code, String(root, "error_description") ?? $"The token endpoint refused the request ({code}).");
        }

        var access = String(root, "access_token");
        if (string.IsNullOrEmpty(access))
        {
            return new OAuthTokenResult(null, "invalid_response", "The token endpoint returned no access_token.");
        }

        var expiresIn = Int(root, "expires_in") ?? 3600;
        var scopes = String(root, "scope")?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new OAuthTokenResult(new OAuthTokenSet(
            access,
            String(root, "refresh_token") ?? keepRefreshToken,
            DateTime.UtcNow.AddSeconds(Math.Max(0, expiresIn)),
            String(root, "token_type") ?? "Bearer",
            scopes,
            String(root, "id_token")));
    }

    private static async Task<(bool Ok, JsonElement? Json)> PostAsync(HttpClient http, string url, Dictionary<string, string> form, CancellationToken cancellationToken, string? basic = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Accept.ParseAdd("application/json");
        if (basic is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);
        }
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaxResponseBytes + 1];
        var read = 0;
        int n;
        while (read <= MaxResponseBytes && (n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancellationToken)) > 0)
        {
            read += n;
        }

        if (read == 0 || read > MaxResponseBytes)
        {
            return (response.IsSuccessStatusCode, null);
        }

        try
        {
            using var document = JsonDocument.Parse(buffer.AsMemory(0, read));
            return (response.IsSuccessStatusCode, document.RootElement.Clone());
        }
        catch (JsonException)
        {
            // Some providers answer form-encoded.
            var parsed = QueryHelpers.ParseQuery(Encoding.UTF8.GetString(buffer, 0, read));
            var obj = parsed.ToDictionary(p => p.Key, p => p.Value.ToString());
            return (response.IsSuccessStatusCode, obj.Count == 0 ? null : JsonSerializer.SerializeToElement(obj));
        }
    }

    private static string? String(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Int(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var text) => text,
            _ => null,
        };
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
