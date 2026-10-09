using System.Text.Json.Nodes;
using Crest.Media.Models;
using Crest.Media.Services;
using Crest.Recipes.Models;
using Crest.Recipes.Services;

namespace Crest.Media.Recipes;

/// <summary>
/// This recipe step creates or updates a media profile.
/// </summary>
public sealed class MediaProfileStep : NamedRecipeStepHandler
{
    private readonly MediaProfilesManager _mediaProfilesManager;

    public MediaProfileStep(MediaProfilesManager mediaProfilesManager)
        : base("MediaProfiles")
    {
        _mediaProfilesManager = mediaProfilesManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<MediaProfileStepModel>();

        foreach (var mediaProfile in model.MediaProfiles)
        {
            await _mediaProfilesManager.UpdateMediaProfileAsync(mediaProfile.Key, mediaProfile.Value);
        }
    }
}

public sealed class MediaProfileStepModel
{
    public Dictionary<string, MediaProfile> MediaProfiles { get; set; }
}
