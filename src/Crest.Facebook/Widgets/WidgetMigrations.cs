using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Settings;
using Crest.Data.Migration;
using Crest.Facebook.Widgets.Models;
using Crest.Recipes;
using Crest.Recipes.Services;

namespace Crest.Facebook.Widgets;

public sealed class WidgetMigrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public WidgetMigrations(
        IRecipeMigrator recipeMigrator,
        IContentDefinitionManager contentDefinitionManager)
    {
        _recipeMigrator = recipeMigrator;
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(FacebookPluginPart), builder => builder
            .Attachable()
            .WithDescription("Provides a Facebook plugin part to create Facebook social plugin widgets."));

        await _recipeMigrator.ExecuteAsync($"Widgets/migration{RecipesConstants.RecipeExtension}", this);

        return 1;
    }
}
