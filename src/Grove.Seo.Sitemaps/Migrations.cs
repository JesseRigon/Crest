using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using Grove.Contents.Types;

namespace Grove.Seo.Sitemaps;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("Sitemap", builder => builder
            .Attachable()
            .WithDescription("Provides an optional part that allows content items to be excluded, or configured, on a content item."));

        await DefineSitemapFieldsAsync();

        return 2;
    }

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows); the ones its
    // system flows require are locked by the module.
    public async Task<int> UpdateFrom1Async()
    {
        await DefineSitemapFieldsAsync();
        return 2;
    }

    private Task DefineSitemapFieldsAsync() =>
        _contentDefinitionManager.AlterFieldSetDefinitionAsync("Sitemap", builder => builder
            .WithField("OverrideSitemapConfig", field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Override the sitemap configuration")
                .WithPosition("0")
                .WithEditor(ContentFieldEditors.None))
            .WithField("Exclude", field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Exclude from the sitemap")
                .WithPosition("1")
                .WithEditor(ContentFieldEditors.None))
            .WithField("ChangeFrequency", field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Change frequency")
                .WithPosition("2")
                .WithEditor(ContentFieldEditors.None))
            .WithField("Priority", field => field
                .OfType("NumericField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Priority")
                .WithPosition("3")
                .WithEditor(ContentFieldEditors.None)));
}
