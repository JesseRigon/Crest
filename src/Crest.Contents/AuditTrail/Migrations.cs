using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Settings;
using Crest.AuditTrail.Models;
using Crest.Contents.AuditTrail.Indexes;
using Crest.Data.Migration;
using YesSql.Sql;
using Crest.Modules;

namespace Crest.Contents.AuditTrail;

[RequireFeatures("Crest.AuditTrail")]
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("AuditTrailPart", part => part
            .Attachable()
            .WithDescription("Allows editors to enter a comment to be saved into the Audit Trail event when saving a content item."));

        // Edited in place (pre-release): content history as diffs (docs/audit.md).
        await SchemaBuilder.CreateMapIndexTableAsync<ContentHistoryIndex>(table => table
            .Column<string>(nameof(ContentHistoryIndex.EventId), column => column.WithLength(26))
            .Column<string>(nameof(ContentHistoryIndex.ContentItemId), column => column.WithLength(26))
            .Column<int>(nameof(ContentHistoryIndex.Sequence))
            .Column<bool>(nameof(ContentHistoryIndex.HasFullState))
            .Column<bool>(nameof(ContentHistoryIndex.IsPublishedSnapshot))
            .Column<DateTime>(nameof(ContentHistoryIndex.CreatedUtc), column => column.Nullable()),
            collection: AuditTrailEvent.Collection);

        await SchemaBuilder.AlterIndexTableAsync<ContentHistoryIndex>(table =>
            table.CreateIndex("IDX_ContentHistoryIndex_Item", "DocumentId", "ContentItemId", "Sequence", "HasFullState", "IsPublishedSnapshot"),
            collection: AuditTrailEvent.Collection);

        return 1;
    }
}
