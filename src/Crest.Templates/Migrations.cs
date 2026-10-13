using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Settings;
using Crest.Data.Migration;
using Crest.Templates.Models;

namespace Crest.Templates;

/// <summary>
/// The <see cref="TemplatesConstants.ContentType"/> type (drafts, publish and history, edited on
/// the Templates page) and the <see cref="DesignedPagesConstants.ContentType"/> type (decision 9).
/// </summary>
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

        await CreateDesignedPagesAsync();
        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await CreateDesignedPagesAsync();
        return 2;
    }

    // Drafts and publish, no overrides: a page record is edited directly (decision 9).
    private async Task CreateDesignedPagesAsync()
    {
        await contentDefinitionManager.AlterPartDefinitionAsync(nameof(DesignedPagePart), part => part
            .WithDescription("A designed page: a route in a shell bucket and the Liquid tree it renders."));

        await contentDefinitionManager.AlterTypeDefinitionAsync(DesignedPagesConstants.ContentType, type => type
            .DisplayedAs("Designed page")
            .WithPart(nameof(DesignedPagePart))
            .Draftable()
            .Versionable()
            .Securable());
    }
}
