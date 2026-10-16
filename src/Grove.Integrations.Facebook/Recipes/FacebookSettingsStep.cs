using System.Text.Json.Nodes;
using Grove.Integrations.Facebook.Services;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Recipes.Services;

namespace Grove.Integrations.Facebook.Recipes;

/// <summary>
/// Sets the Meta app: the app id and secret go to the Meta app credentials (sealed by the
/// connector engine), the SDK options to the Meta settings.
/// </summary>
public sealed class FacebookSettingsStep : NamedRecipeStepHandler
{
    private readonly IFacebookService _facebookService;
    private readonly IServiceProvider _services;

    public FacebookSettingsStep(IFacebookService facebookService, IServiceProvider services)
        : base("FacebookCoreSettings")
    {
        _facebookService = facebookService;
        _services = services;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<FacebookCoreSettingsStepModel>();
        var settings = await _facebookService.GetSettingsAsync();

        settings.SdkJs = model.SdkJs ?? "sdk.js";
        settings.FBInit = model.FBInit;
        settings.FBInitParams = model.FBInitParams;
        settings.Version = model.Version ?? "v3.2";

        await _facebookService.UpdateSettingsAsync(settings);
        await AppCredentialsAccess.SaveAsync(_services, FacebookConstants.AppCredentialsKey, model.AppId, model.AppSecret);
    }
}

public sealed class FacebookCoreSettingsStepModel
{
    public string AppId { get; set; }
    public string AppSecret { get; set; }
    public string SdkJs { get; set; }
    public bool FBInit { get; set; }
    public string FBInitParams { get; set; }
    public string Version { get; set; }
}
