using System.Text.Json.Nodes;
using Crest.Facebook.Login.Services;
using Crest.Facebook.Login.Settings;
using Crest.Recipes.Models;
using Crest.Recipes.Services;

namespace Crest.Facebook.Login.Recipes;

/// <summary>
/// This recipe step sets general Facebook Login settings.
/// </summary>
public sealed class FacebookLoginSettingsStep : NamedRecipeStepHandler
{
    private readonly IFacebookLoginService _loginService;

    public FacebookLoginSettingsStep(IFacebookLoginService loginService)
        : base(nameof(FacebookLoginSettings))
    {
        _loginService = loginService;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<FacebookLoginSettingsStepModel>();
        var settings = await _loginService.LoadSettingsAsync();

        settings.CallbackPath = model.CallbackPath;

        await _loginService.UpdateSettingsAsync(settings);
    }
}

public sealed class FacebookLoginSettingsStepModel
{
    public string CallbackPath { get; set; }
}
