#nullable enable

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Grove.Api;
using Grove.Access.OpenId.Abstractions.Managers;
using Grove.Access.OpenId.Services;

namespace Grove.Access.OpenId.Api;

/// <summary>What the consent page shows and how its form posts back to the authorization endpoint.</summary>
public sealed record OpenIdConsentModel(string ApplicationName, string[] Scopes, string Action, string AntiforgeryFieldName, string AntiforgeryToken);

/// <summary>How the logout confirmation's form posts back to the logout endpoint.</summary>
public sealed record OpenIdLogoutModel(string Action, string AntiforgeryFieldName, string AntiforgeryToken);

/// <summary>
/// The consent and logout confirmation pages' data (step 7: the MVC Authorize and Logout views
/// are retired; the pages are the site shell's). The decision itself stays a form post to the
/// protocol endpoint - OpenIddict answers the authorization request in that request - so this
/// only names the application and scopes and hands the page the antiforgery token the Accept
/// action requires.
/// </summary>
public sealed class OpenIdConsentApi : ApiContributor
{
    public override string Name => "OpenIdConsent";

    public override string FeatureId => OpenIdConstants.Features.Server;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("openid/consent");
        routes.MapGet("", (HttpContext http, [FromQuery(Name = "client_id")] string? clientId, [FromQuery] string? scope) => ApiHandlers.For<OpenIdConsentHandlers>(http).GetAsync(clientId, scope))
            .RequireAuthorization();
        routes.MapGet("logout", ApiHandlers.Handle<OpenIdConsentHandlers>(h => h.GetLogoutAsync()))
            .AllowAnonymous();
    }
}

public sealed class OpenIdConsentHandlers(
    IOpenIdApplicationManager applications,
    IOpenIdScopeManager scopes,
    IOpenIdServerService server,
    IAntiforgery antiforgery,
    IOptions<AntiforgeryOptions> antiforgeryOptions) : ApiHandlers
{
    public async Task<IResult> GetAsync(string? clientId, string? scope)
    {
        if (string.IsNullOrEmpty(clientId) || await applications.FindByClientIdAsync(clientId) is not { } application)
        {
            return NotFound();
        }

        var names = new List<string>();
        foreach (var name in (scope ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            names.Add(await scopes.FindByNameAsync(name) is { } known ? await scopes.GetLocalizedDisplayNameAsync(known) ?? name : name);
        }

        var settings = await server.GetSettingsAsync();
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new OpenIdConsentModel(
            await applications.GetLocalizedDisplayNameAsync(application) ?? clientId,
            [.. names],
            $"{Request.PathBase}{settings.AuthorizationEndpointPath}",
            antiforgeryOptions.Value.FormFieldName,
            tokens.RequestToken ?? string.Empty));
    }

    public async Task<IResult> GetLogoutAsync()
    {
        var settings = await server.GetSettingsAsync();
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new OpenIdLogoutModel($"{Request.PathBase}{settings.LogoutEndpointPath}", antiforgeryOptions.Value.FormFieldName, tokens.RequestToken ?? string.Empty));
    }
}
