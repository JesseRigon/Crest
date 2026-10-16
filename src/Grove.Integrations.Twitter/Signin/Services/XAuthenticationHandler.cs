#nullable enable

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.Twitter.Signin.Services;

/// <summary>Sign in with X over OAuth 2.0 (authorization code with PKCE), on the app credentials the X connection shares.</summary>
public sealed class XAuthenticationOptions : OAuthOptions
{
    public XAuthenticationOptions()
    {
        AuthorizationEndpoint = TwitterConstants.Endpoints.Authorize;
        TokenEndpoint = TwitterConstants.Endpoints.Token;
        UserInformationEndpoint = TwitterConstants.Endpoints.Me;
        CallbackPath = "/signin-x";
        UsePkce = true;
        Scope.Add("users.read");
        Scope.Add("tweet.read");

        ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
        ClaimActions.MapJsonKey("urn:x:name", "name");
    }
}

/// <summary>
/// The OAuth 2.0 handler for X: a confidential X client authenticates at the token endpoint with
/// HTTP Basic, and the user comes from <c>/2/users/me</c>'s <c>data</c>.
/// </summary>
public sealed class XAuthenticationHandler(IOptionsMonitor<XAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : OAuthHandler<XAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context)
    {
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = context.Code,
            ["redirect_uri"] = context.RedirectUri,
            ["client_id"] = Options.ClientId,
        };

        if (context.Properties.Items.TryGetValue(OAuthConstants.CodeVerifierKey, out var codeVerifier) && codeVerifier is not null)
        {
            parameters[OAuthConstants.CodeVerifierKey] = codeVerifier;
            context.Properties.Items.Remove(OAuthConstants.CodeVerifierKey);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(parameters),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{Uri.EscapeDataString(Options.ClientId)}:{Uri.EscapeDataString(Options.ClientSecret)}")));

        using var response = await Backchannel.SendAsync(request, Context.RequestAborted);
        var body = await response.Content.ReadAsStringAsync(Context.RequestAborted);

        return response.IsSuccessStatusCode
            ? OAuthTokenResponse.Success(JsonDocument.Parse(body))
            : OAuthTokenResponse.Failed(new AuthenticationFailureException($"X refused the authorization code ({(int)response.StatusCode})."));
    }

    protected override async Task<AuthenticationTicket> CreateTicketAsync(ClaimsIdentity identity, AuthenticationProperties properties, OAuthTokenResponse tokens)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Options.UserInformationEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var response = await Backchannel.SendAsync(request, Context.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            throw new AuthenticationFailureException($"X did not return the signed-in user ({(int)response.StatusCode}).");
        }

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Context.RequestAborted));
        var user = payload.RootElement.GetProperty("data");

        var context = new OAuthCreatingTicketContext(new ClaimsPrincipal(identity), properties, Context, Scheme, Options, Backchannel, tokens, user);
        context.RunClaimActions();
        await Events.CreatingTicket(context);

        return new AuthenticationTicket(context.Principal!, context.Properties, Scheme.Name);
    }
}
