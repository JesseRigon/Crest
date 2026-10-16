using Grove.Data.Connectors.Api;

namespace Grove.Data.Connectors.BlazorWasm;

/// <summary>What a credential form asks for: the values an auth scheme takes, plus the protocol's own credential fields.</summary>
public static class CredentialForm
{
    public const string ApiRoot = "api/grove/connections";

    public static readonly IReadOnlyList<string> AuthSchemes =
        ["none", "api-key", "basic", "bearer", "oauth2-client-credentials", "oauth2-authorization-code", "oauth2-device-code", "hmac", "custom"];

    /// <summary>The values a credential of <paramref name="scheme"/> holds in <paramref name="scope"/>. A user grant's tokens come from authorizing, never from a form.</summary>
    public static IReadOnlyList<SettingFieldModel> Fields(string scheme, string scope, bool sharedAppCredentials, ProtocolModel? protocol)
    {
        var fields = new List<SettingFieldModel>();
        switch (scheme)
        {
            case "api-key":
                fields.Add(Secret("apiKey", "API key"));
                break;
            case "basic":
                fields.Add(new("username", "User name", "text", true, null, null, null));
                fields.Add(Secret("password", "Password"));
                break;
            case "bearer":
                fields.Add(Secret("token", "Token"));
                break;
            case "oauth2-client-credentials" or "oauth2-authorization-code" or "oauth2-device-code" when scope == "tenant" && !sharedAppCredentials:
                fields.Add(new("clientId", "Client id", "text", true, null, null, null));
                fields.Add(Secret("clientSecret", "Client secret"));
                break;
            case "hmac":
                fields.Add(Secret("signingSecret", "Signing secret"));
                break;
        }

        foreach (var field in protocol?.CredentialFields ?? [])
        {
            if (fields.All(f => !string.Equals(f.Name, field.Name, StringComparison.OrdinalIgnoreCase)))
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    /// <summary>A query string value of <paramref name="uri"/>, unescaped; null when absent.</summary>
    public static string? QueryValue(string uri, string name)
    {
        foreach (var pair in new Uri(uri).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (Uri.UnescapeDataString(parts[0]) == name)
            {
                return parts.Length == 2 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : string.Empty;
            }
        }

        return null;
    }

    public static bool IsInteractive(string scheme) => scheme is "oauth2-authorization-code" or "oauth2-device-code";

    private static SettingFieldModel Secret(string name, string label) => new(name, label, "secret", true, null, null, null);
}
