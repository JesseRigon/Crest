using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Settings;
using Crest.Data.Migration;
using Crest.Templates.Models;

namespace Crest.Templates;

/// <summary>The <see cref="TemplatesConstants.ContentType"/> type: drafts, publish and history, edited on the Templates page.</summary>
public sealed class Migrations(IContentDefinitionManager contentDefinitionManager) : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await contentDefinitionManager.AlterPartDefinitionAsync(nameof(TemplatePart), part => part
            .WithDescription("A Liquid template that emits display-tree nodes."));

        await contentDefinitionManager.AlterTypeDefinitionAsync(TemplatesConstants.ContentType, type => type
            .DisplayedAs("Template")
            .WithPart(nameof(TemplatePart))
            .Draftable()
            .Versionable()
            .Securable());

        return 1;
    }
}
