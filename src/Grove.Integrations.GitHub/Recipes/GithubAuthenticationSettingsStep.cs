using System.Text.Json.Nodes;
using Grove.Entities;
using Grove.Integrations.GitHub.Services;
using Grove.Integrations.GitHub.Settings;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Recipes.Services;
using Grove.Infrastructure.Settings;

namespace Grove.Integrations.GitHub.Recipes;

/// <summary>
/// Sets GitHub sign-in: the client id and secret go to the GitHub app credentials (sealed by the
/// connector engine), the callback path to the sign-in settings.
/// </summary>
public sealed class GitHubAuthenticationSettingsStep : NamedRecipeStepHandler
{
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _services;

    public GitHubAuthenticationSettingsStep(ISiteService siteService, IServiceProvider services)
        : base(nameof(GitHubAuthenticationSettings))
    {
        _siteService = siteService;
        _services = services;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<GitHubLoginSettingsStepModel>();
        var site = await _siteService.LoadSiteSettingsAsync();

        site.Alter<GitHubAuthenticationSettings>(settings => settings.CallbackPath = model.CallbackPath);

        await _siteService.UpdateSiteSettingsAsync(site);
        await AppCredentialsAccess.SaveAsync(_services, GitHubConstants.AppCredentialsKey, model.ConsumerKey, model.ConsumerSecret);
    }
}

public sealed class GitHubLoginSettingsStepModel
{
    public string ConsumerKey { get; set; }

    public string ConsumerSecret { get; set; }

    public string CallbackPath { get; set; }
}
