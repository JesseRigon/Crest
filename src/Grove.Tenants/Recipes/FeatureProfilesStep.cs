using System.Text.Json.Nodes;
using Grove.Environment.Shell.Models;
using Grove.Recipes.Models;
using Grove.Recipes.Services;
using Grove.Tenants.Services;

namespace Grove.Tenants.Recipes;

/// <summary>
/// This recipe step creates a set of feature profiles.
/// </summary>
public sealed class FeatureProfilesStep : NamedRecipeStepHandler
{
    private readonly FeatureProfilesManager _featureProfilesManager;

    public FeatureProfilesStep(FeatureProfilesManager featureProfilesManager)
        : base("FeatureProfiles")
    {
        _featureProfilesManager = featureProfilesManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step.TryGetPropertyValue("FeatureProfiles", out var jsonNode) && jsonNode is JsonObject featureProfiles)
        {
            foreach (var property in featureProfiles)
            {
                var name = property.Key;
                var value = property.Value.ToObject<FeatureProfile>();

                await _featureProfilesManager.UpdateFeatureProfileAsync(name, value);
            }
        }
    }
}
