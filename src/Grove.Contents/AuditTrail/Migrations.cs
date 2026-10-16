using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.AuditTrail.Models;
using Grove.Contents.AuditTrail.Indexes;
using Grove.Data.Migration;
using YesSql.Sql;
using Grove.Modules;
using Grove.Contents.AuditTrail.Models;

namespace Grove.Contents.AuditTrail;

[RequireFeatures("Grove.AuditTrail")]
public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("AuditTrail", part => part
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

        await DefineAuditTrailFieldsAsync();

        return 2;
    }

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows).
    public async Task<int> UpdateFrom1Async()
    {
        await DefineAuditTrailFieldsAsync();
        return 2;
    }

    private Task DefineAuditTrailFieldsAsync() =>
        _contentDefinitionManager.AlterFieldSetDefinitionAsync("AuditTrail", builder => builder
            .WithField(nameof(AuditTrailFieldSet.Comment), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Comment")
                .WithPosition("0")
                .WithEditor("TextArea")));
}
