using Grove.Contents.Management;
using Grove.Seo.Models;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using Grove.Media.Settings;
using Grove.Infrastructure.Recipes;
using Grove.Infrastructure.Recipes.Services;
using Grove.Contents.Types;

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
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("SeoMeta", builder => builder
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

        await DefineSeoMetaFieldsAsync();

        return 3;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await _recipeMigrator.ExecuteAsync($"socialmetasettings{RecipesConstants.RecipeExtension}", this);

        return 2;
    }

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows); the ones its
    // system flows require are locked by the module.
    public async Task<int> UpdateFrom2Async()
    {
        await DefineSeoMetaFieldsAsync();
        return 3;
    }

    private Task DefineSeoMetaFieldsAsync() =>
        _contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<SeoMetaFieldSet>(), builder => builder
            .WithField("PageTitle", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Page title")
                .WithPosition("0")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("Render", field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Render meta tags")
                .WithPosition("1")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("MetaDescription", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Meta description")
                .WithPosition("2")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("MetaKeywords", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Meta keywords")
                .WithPosition("3")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("Canonical", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Canonical address")
                .WithPosition("4")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("MetaRobots", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Meta robots")
                .WithPosition("5")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("DefaultSocialImage", field => field
                .OfType("MediaField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Default social image")
                .WithPosition("6")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("OpenGraphType", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Open Graph type")
                .WithPosition("7")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("OpenGraphTitle", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Open Graph title")
                .WithPosition("8")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("OpenGraphDescription", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Open Graph description")
                .WithPosition("9")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("OpenGraphImage", field => field
                .OfType("MediaField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Open Graph image")
                .WithPosition("10")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("TwitterCard", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Twitter card")
                .WithPosition("11")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("TwitterCreator", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Twitter creator")
                .WithPosition("12")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("TwitterSite", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Twitter site")
                .WithPosition("13")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("TwitterTitle", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Twitter title")
                .WithPosition("14")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("TwitterDescription", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Twitter description")
                .WithPosition("15")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("TwitterImage", field => field
                .OfType("MediaField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Twitter image")
                .WithPosition("16")
                .WithEditor(ContentFieldEditors.None)
                .Locked())
            .WithField("GoogleSchema", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Google schema")
                .WithPosition("17")
                .WithEditor(ContentFieldEditors.None)
                .Locked()));
}
