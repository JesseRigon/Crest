using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Settings;
using Grove.Data.Migration;
using Grove.Media.Settings;
using Grove.Recipes;
using Grove.Recipes.Services;

namespace Grove.Seo;

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
