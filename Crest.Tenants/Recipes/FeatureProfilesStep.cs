using System.Text.Json.Nodes;
using Crest.Environment.Shell.Models;
using Crest.Recipes.Models;
using Crest.Recipes.Services;
using Crest.Tenants.Services;

namespace Crest.Tenants.Recipes;

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
