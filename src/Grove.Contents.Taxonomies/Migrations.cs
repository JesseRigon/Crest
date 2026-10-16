using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using Grove.Contents.Taxonomies.Models;
using Grove.Contents.Types;

namespace Grove.Contents.Taxonomies;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Taxonomy", taxonomy => taxonomy
            .WithDisplayName("Categories")
            .Draftable()
            .Versionable()
            .Creatable()
            .Listable()
            .WithFieldSet("Title", part => part.WithPosition("1"))
            .WithFieldSet("Alias", part => part
                .WithPosition("2")
                .WithSettings(new AliasFieldSetSettings
                {
                    Pattern = "{{ Model.ContentItem | display_text | slugify }}",
                }))
            .WithFieldSet("Autoroute", part => part
                .WithPosition("3")
                .WithSettings(new AutorouteFieldSetSettings
                {
                    Pattern = "{{ Model.ContentItem | display_text | slugify }}",
                    AllowRouteContainedItems = true,
                }))
            .WithFieldSet("Taxonomy", part => part.WithPosition("4"))
        );

        await DeclareTermsAsync();

        await DefineTaxonomyTermFieldsAsync();
        await LabelAsCategoriesAsync();

        // Shortcut other migration steps on new content definition schemas.
        return 8;
    }

    // The terms are the taxonomy's embedding (docs/content-engine.md › Embedded content items):
    // nested (each term holds its children), referenceable (option list fields on
    // embedded:Taxonomy.Terms and routes point at them), of the type the taxonomy names.
    public async Task<int> UpdateFrom5Async()
    {
        await DeclareTermsAsync();
        return 6;
    }

    private Task DeclareTermsAsync() =>
        _contentDefinitionManager.AlterTypeDefinitionAsync("Taxonomy", taxonomy => taxonomy
            .WithEmbedding(TaxonomyFieldSet.TermsEmbedding, terms => terms
                .WithDisplayName("Terms")
                .WithPosition("5")
                .Allow(Grove.Contents.Management.Metadata.Models.EmbeddedContentDefinition.AnyType)
                .Nested()
                .Referenceable()));

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows); the ones its
    // system flows require are locked by the module.
    public async Task<int> UpdateFrom6Async()
    {
        await DefineTaxonomyTermFieldsAsync();
        return 7;
    }

    // Taxonomies are "Categories" on every screen; the code keeps its names.
    public async Task<int> UpdateFrom7Async()
    {
        await DefineTaxonomyTermFieldsAsync();
        await LabelAsCategoriesAsync();
        return 8;
    }

    private async Task LabelAsCategoriesAsync()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Taxonomy", taxonomy => taxonomy.WithDisplayName("Categories"));
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("Taxonomy", builder => builder
            .WithDisplayName("Categories")
            .WithDescription("Makes the item a list of categories (a taxonomy): its terms are what content is filed under."));
    }

    private async Task DefineTaxonomyTermFieldsAsync()
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("Taxonomy", builder => builder
            .WithField(TaxonomyFieldSet.TermContentTypeField, field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Term type")
                .WithPosition("0")
                .WithEditor(ContentFieldEditors.None)
                .Locked()));

        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("Term", builder => builder
            .WithField(TermFieldSet.TaxonomyContentItemIdField, field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Categories")
                .WithPosition("0")
                .WithEditor(ContentFieldEditors.None)
                .Locked()));
    }
}

internal sealed class AliasFieldSetSettings
{
    public string Pattern { get; set; }
}

internal sealed class AutorouteFieldSetSettings
{
    public string Pattern { get; set; }
    public bool AllowRouteContainedItems { get; set; }
}
