using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using Grove.Contents.Markdown.Fields;
using Grove.Contents.Markdown.Settings;
using Grove.Contents.Types;

namespace Grove.Contents.Markdown;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("MarkdownBody", builder => builder
            .Attachable()
            .WithDescription("Provides a Markdown formatted body for your content item."));

        await DefineMarkdownBodyFieldsAsync();

        // Shortcut other migration steps on new content definition schemas.
        return 6;
    }

    // Migrate FieldSettings. This only needs to run on old content definition schemas.
    // This code can be removed in a later version.
    public async Task<int> UpdateFrom1Async()
    {
        await _contentDefinitionManager.MigrateFieldSettingsAsync<MarkdownField, MarkdownFieldSettings>();

        return 4; // Returning 4 instead of 2 to skip the next 2 migration steps, see below why.
    }

    // Previously, Liquid rendering was enabled by not having Html sanitization enabled and UpdateFrom2Async and
    // UpdateFrom3Async disabled sanitization to ensure that MarkdownBodyParts and MarkdownFields kept Liquid rendering
    // enabled. Since Liquid rendering is now controlled by a separate setting, disabling sanitization is no longer
    // necessary.

    public async Task<int> UpdateFrom4Async()
    {
        // To keep the same behavior as before, RenderLiquid is initialized to the opposite of SanitizeHtml.
        foreach (var contentType in await _contentDefinitionManager.LoadTypeDefinitionsAsync())
        {
            if (contentType.FieldSets.Any(x => x.FieldSetDefinition.Name == "MarkdownBody"))
            {
                await _contentDefinitionManager.AlterTypeDefinitionAsync(contentType.Name, x => x.WithFieldSet("MarkdownBody", part =>
                {
                    part.MergeSettings<MarkdownBodyFieldSetSettings>(s => s.RenderLiquid = !s.SanitizeHtml);
                }));
            }
        }

        var partDefinitions = await _contentDefinitionManager.LoadFieldSetDefinitionsAsync();
        foreach (var partDefinition in partDefinitions)
        {
            if (partDefinition.Fields.Any(x => x.FieldTypeDefinition.Name == "MarkdownField"))
            {
                await _contentDefinitionManager.AlterFieldSetDefinitionAsync(partDefinition.Name, partBuilder =>
                {
                    foreach (var fieldDefinition in partDefinition.Fields.Where(x => x.FieldTypeDefinition.Name == "MarkdownField"))
                    {
                        partBuilder.WithField(fieldDefinition.Name, fieldBuilder =>
                        {
                            fieldBuilder.MergeSettings<MarkdownFieldSettings>(s => s.RenderLiquid = !s.SanitizeHtml);
                        });
                    }
                });
            }
        }

        return 5;
    }

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows); the ones its
    // system flows require are locked by the module.
    public async Task<int> UpdateFrom5Async()
    {
        await DefineMarkdownBodyFieldsAsync();
        return 6;
    }

    private Task DefineMarkdownBodyFieldsAsync() =>
        _contentDefinitionManager.AlterFieldSetDefinitionAsync("MarkdownBody", builder => builder
            .WithField(Models.MarkdownBodyFieldSet.MarkdownField, field => field
                .OfType("MarkdownField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Body")
                .WithPosition("0")
                .Locked()));
}
