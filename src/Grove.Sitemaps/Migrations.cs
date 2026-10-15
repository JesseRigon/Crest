using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Settings;
using Grove.Data.Migration;

namespace Grove.Sitemaps;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("SitemapPart", builder => builder
            .Attachable()
            .WithDescription("Provides an optional part that allows content items to be excluded, or configured, on a content item."));

        return 1;
    }
}
