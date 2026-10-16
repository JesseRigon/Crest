using System.Text.Json.Nodes;
using Grove.Integrations.Microsoft.Services;
using Grove.Integrations.Microsoft.Settings;
using Grove.Infrastructure.Recipes.Models;
using Grove.Infrastructure.Recipes.Services;

namespace Grove.Integrations.Microsoft.Recipes;

/// <summary>
/// This recipe step sets general Microsoft Entra ID settings.
/// </summary>
public sealed class AzureADSettingsStep : NamedRecipeStepHandler
{
    private readonly IAzureADService _azureADService;
    private readonly IServiceProvider _services;

    public AzureADSettingsStep(IAzureADService azureADService, IServiceProvider services)
        : base(nameof(AzureADSettings))
    {
        _azureADService = azureADService;
        _services = services;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<AzureADSettingsStepModel>();
        var settings = await _azureADService.LoadSettingsAsync();

        settings.TenantId = model.TenantId;
        settings.DisplayName = model.DisplayName;
        settings.CallbackPath = model.CallbackPath;

        await _azureADService.UpdateSettingsAsync(settings);

        // The application id is the Microsoft app credentials' client id.
        await AppCredentialsAccess.SaveAsync(_services, MicrosoftAuthenticationConstants.AppCredentialsKey, model.AppId, null);
    }
}

public sealed class AzureADSettingsStepModel
{
    public string DisplayName { get; set; }
    public string AppId { get; set; }
    public string TenantId { get; set; }
    public string CallbackPath { get; set; }
}
