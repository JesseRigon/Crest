using System.Text.Json.Nodes;
using Grove.Integrations.Microsoft.Services;
using Grove.Integrations.Microsoft.Settings;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Recipes.Services;

namespace Grove.Integrations.Microsoft.Recipes;

/// <summary>
/// This recipe step sets Microsoft Account settings.
/// </summary>
public sealed class MicrosoftAccountSettingsStep : NamedRecipeStepHandler
{
    private readonly IMicrosoftAccountService _microsoftAccountService;
    private readonly IServiceProvider _services;

    public MicrosoftAccountSettingsStep(IMicrosoftAccountService microsoftAccountService, IServiceProvider services)
        : base(nameof(MicrosoftAccountSettings))
    {
        _microsoftAccountService = microsoftAccountService;
        _services = services;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<MicrosoftAccountSettingsStepModel>();
        var settings = await _microsoftAccountService.LoadSettingsAsync();

        settings.CallbackPath = model.CallbackPath;

        await _microsoftAccountService.UpdateSettingsAsync(settings);
        await AppCredentialsAccess.SaveAsync(_services, MicrosoftAuthenticationConstants.AppCredentialsKey, model.AppId, model.AppSecret);
    }
}

public sealed class MicrosoftAccountSettingsStepModel
{
    public string AppId { get; set; }
    public string AppSecret { get; set; }
    public string CallbackPath { get; set; }
}
