using System.Text.Json.Nodes;
using Crest.OpenId.Abstractions.Managers;
using Crest.Recipes.Models;
using Crest.Recipes.Services;

namespace Crest.OpenId.Recipes;

public sealed class OpenIdApplicationStep : NamedRecipeStepHandler
{
    private readonly IOpenIdApplicationManager _applicationManager;

    /// <summary>
    /// This recipe step adds an OpenID Connect app.
    /// </summary>
    public OpenIdApplicationStep(IOpenIdApplicationManager applicationManager)
        : base("OpenIdApplication")
    {
        _applicationManager = applicationManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<OpenIdApplicationStepModel>();
        var app = await _applicationManager.FindByClientIdAsync(model.ClientId);

        var settings = new OpenIdApplicationSettings()
        {
            AllowAuthorizationCodeFlow = model.AllowAuthorizationCodeFlow,
            AllowClientCredentialsFlow = model.AllowClientCredentialsFlow,
            AllowHybridFlow = model.AllowHybridFlow,
            AllowImplicitFlow = model.AllowImplicitFlow,
            AllowIntrospectionEndpoint = model.AllowIntrospectionEndpoint,
            AllowLogoutEndpoint = model.AllowLogoutEndpoint,
            AllowPasswordFlow = model.AllowPasswordFlow,
            AllowRefreshTokenFlow = model.AllowRefreshTokenFlow,
            AllowRevocationEndpoint = model.AllowRevocationEndpoint,
            ClientId = model.ClientId,
            ClientSecret = model.ClientSecret,
            ConsentType = model.ConsentType,
            DisplayName = model.DisplayName,
            PostLogoutRedirectUris = model.PostLogoutRedirectUris,
            RedirectUris = model.RedirectUris,
            Roles = model.RoleEntries.Select(x => x.Name).ToArray(),
            Scopes = model.ScopeEntries.Select(x => x.Name).ToArray(),
            Type = model.Type,
            RequireProofKeyForCodeExchange = model.RequireProofKeyForCodeExchange,
            RequirePushedAuthorizationRequests = model.RequirePushedAuthorizationRequests,
        };

        await _applicationManager.UpdateDescriptorFromSettings(settings, app);
    }
}
