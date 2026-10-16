using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Contents.Embedding;
using Grove.Data.Migration;
using YesSql.Sql;

namespace Grove.Contents;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("Common", builder => builder
            .Attachable()
            .WithDescription("Provides an editor for the common properties of a content item."));

        await CreateEmbeddedContentItemIndexAsync();

        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await CreateEmbeddedContentItemIndexAsync();

        return 2;
    }

    // The embedded-item index (docs/content-engine.md › Embedded content items).
    private async Task CreateEmbeddedContentItemIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<EmbeddedContentItemIndex>(table => table
            .Column<string>("ParentContentItemId", column => column.WithLength(26))
            .Column<string>("EmbeddingName", column => column.WithLength(255))
            .Column<string>("ContentItemId", column => column.WithLength(50))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<int>("Position")
            .Column<bool>("Published")
            .Column<bool>("Latest"));

        await SchemaBuilder.AlterIndexTableAsync<EmbeddedContentItemIndex>(table => table
            .CreateIndex("IDX_EmbeddedContentItemIndex_Item", "DocumentId", "ContentItemId", "Latest", "Published"));
        await SchemaBuilder.AlterIndexTableAsync<EmbeddedContentItemIndex>(table => table
            .CreateIndex("IDX_EmbeddedContentItemIndex_Parent", "DocumentId", "ParentContentItemId", "EmbeddingName"));
    }
}
