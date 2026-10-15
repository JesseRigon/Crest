using System.ComponentModel.DataAnnotations;
using Grove.Settings;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Grove.OpenId.Settings;

public class OpenIdClientSettings
{
    /// <summary>The data protection purpose the client secret is stored under.</summary>
    public const string ClientSecretPurpose = "OpenIdClientConfiguration";

    [Display(Name = "Display Name", Description = "User-Friendly Display Name of the identity provider", Order = 0)]
    public string DisplayName { get; set; }

    [Display(Name = "Authority", Description = "Authority URL for OpenID Connect Requests", Order = 1)]
    [Required]
    public Uri Authority { get; set; }

    [Display(Name = "ClientId", Description = "The 'client_id' assigned by the identity provider.", Order = 2)]
    public string ClientId { get; set; }

    [Display(Name = "Client Secret", Description = "The 'client_secret' assigned by the identity provider; used by the flows that return a code.", Order = 11)]
    [Setting(Secret = true, ProtectedBy = ClientSecretPurpose, VisibleWhen = "ResponseType=code|code id_token|code id_token token|code token")]
    public string ClientSecret { get; set; }

    [Display(Name = "CallbackPath", Description = "The request path, relative to the application's base path, where the user-agent will be redirected. The middleware will handle this request upon arrival.", Prompt = "/signin-oidc", Order = 3)]
    public string CallbackPath { get; set; }

    [Display(Name = "SignedOut Redirect Uri", Description = "The URI to which the user-agent will be redirected after the application signs out from the identity provider. This redirect occurs following the invocation of the SignedOutCallbackPath.", Prompt = "/", Order = 5)]
    public string SignedOutRedirectUri { get; set; }

    [Display(Name = "SignedOut CallbackPath", Description = "The request path within the application's base path where the user-agent will be redirected after signing out from the identity provider.", Prompt = "/signout-callback-oidc", Order = 4)]
    public string SignedOutCallbackPath { get; set; }

    [Display(Name = "Scopes", Description = "Request additional scopes beyond 'openid' and 'profile'.", Order = 6)]
    public IEnumerable<string> Scopes { get; set; }

    [Display(Name = "Response type", Description = "The OpenID Connect flow: the authorization code flow, a hybrid flow or an implicit flow.", Order = 10)]
    [Required]
    [Setting(Options = typeof(OpenIdClientResponseTypes))]
    public string ResponseType { get; set; }

    [Display(Name = "Response Mode", Order = 12)]
    [Setting(Options = typeof(OpenIdClientResponseModes))]
    public string ResponseMode { get; set; }

    [Display(Name = "Store tokens?", Description = "Store external authentication tokens for future use.", Order = 7)]
    public bool StoreExternalTokens { get; set; }

    [Display(Name = "Parameters", Description = "Additional parameters sent with the authorization request.", Order = 13)]
    public ParameterSetting[] Parameters { get; set; } = [];
}

public class ParameterSetting
{
    [Required]
    public string Name { get; set; }

    public string Value { get; set; }
}

/// <summary>The response types the client supports.</summary>
public sealed class OpenIdClientResponseTypes : ISettingOptions
{
    public Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SettingOption>>(
        [
            new(OpenIdConnectResponseType.Code, "Code flow: code"),
            new(OpenIdConnectResponseType.CodeIdToken, "Hybrid flow: code id_token"),
            new(OpenIdConnectResponseType.CodeIdTokenToken, "Hybrid flow: code id_token token"),
            new(OpenIdConnectResponseType.CodeToken, "Hybrid flow: code token"),
            new(OpenIdConnectResponseType.IdToken, "Implicit flow: id_token"),
            new(OpenIdConnectResponseType.IdTokenToken, "Implicit flow: id_token token"),
        ]);
}

/// <summary>The response modes the client supports.</summary>
public sealed class OpenIdClientResponseModes : ISettingOptions
{
    public Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SettingOption>>(
        [
            new(OpenIdConnectResponseMode.FormPost, OpenIdConnectResponseMode.FormPost),
            new(OpenIdConnectResponseMode.Query, OpenIdConnectResponseMode.Query),
        ]);
}
