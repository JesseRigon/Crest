using Crest.Recipes.Models;
using Crest.Recipes.Services;
using Crest.Settings;

namespace Crest.CustomSettings.Recipes;

/// <summary>
/// This recipe step updates the site settings.
/// </summary>
public sealed class CustomSettingsStep : NamedRecipeStepHandler
{
    private readonly ISiteService _siteService;

    public CustomSettingsStep(ISiteService siteService)
        : base("custom-settings")
    {
        _siteService = siteService;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var siteSettings = await _siteService.LoadSiteSettingsAsync();

        var model = context.Step;
        foreach (var customSettings in model)
        {
            if (customSettings.Key == "name")
            {
                continue;
            }

            siteSettings.Properties[customSettings.Key] = customSettings.Value.DeepClone();
        }

        await _siteService.UpdateSiteSettingsAsync(siteSettings);
    }
}
