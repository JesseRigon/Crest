using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Settings;
using Crest.Data.Migration;
using Crest.Media.Settings;
using Crest.Recipes;
using Crest.Recipes.Services;

namespace Crest.Seo;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IRecipeMigrator _recipeMigrator;

    public Migrations(IContentDefinitionManager contentDefinitionManager, IRecipeMigrator recipeMigrator)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _recipeMigrator = recipeMigrator;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("SeoMetaPart", builder => builder
            .Attachable()
            .WithDescription("Provides a part that allows SEO meta descriptions to be applied to a content item.")
            .WithField("DefaultSocialImage", field => field
                .OfType("MediaField")
                .WithDisplayName("Default social image")
                .WithSettings(new MediaFieldSettings { Multiple = false }))
            .WithField("OpenGraphImage", field => field
                .OfType("MediaField")
                .WithDisplayName("Open graph image")
                .WithSettings(new MediaFieldSettings { Multiple = false }))
            .WithField("TwitterImage", field => field
                .OfType("MediaField")
                .WithDisplayName("Twitter image")
                .WithSettings(new MediaFieldSettings { Multiple = false }))
        );

        await _recipeMigrator.ExecuteAsync("socialmetasettings.recipe.json", this);

        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await _recipeMigrator.ExecuteAsync($"socialmetasettings{RecipesConstants.RecipeExtension}", this);

        return 2;
    }
}
