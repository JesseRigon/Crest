using Crest.AuditTrail.Indexes;
using Crest.AuditTrail.Models;
using Crest.Data;
using Crest.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace Crest.AuditTrail;

public sealed class Migrations(ISession session) : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AuditTrailEventIndex>(table => table
            .Column<string>(nameof(AuditTrailEventIndex.EventId), column => column.WithLength(26))
            .Column<string>(nameof(AuditTrailEventIndex.Category), column => column.WithLength(64))
            .Column<string>(nameof(AuditTrailEventIndex.Name), column => column.WithLength(64))
            // 255, not a content item id's 26: Crest events correlate on workflow instance ids,
            // connection keys, role names and access resources too.
            .Column<string>(nameof(AuditTrailEventIndex.CorrelationId), column => column.WithLength(255))
            .Column<string>(nameof(AuditTrailEventIndex.UserId), column => column.WithLength(255))
            .Column<string>(nameof(AuditTrailEventIndex.NormalizedUserName), column => column.Nullable().WithLength(255))
            .Column<DateTime>(nameof(AuditTrailEventIndex.CreatedUtc), column => column.Nullable()),
            collection: AuditTrailEvent.Collection);

        await SchemaBuilder.AlterIndexTableAsync<AuditTrailEventIndex>(table => table
            .CreateIndex("IDX_AuditTrailEventIndex_DocumentId",
                "DocumentId",
                "EventId",
                "Category",
                "Name",
                "CorrelationId",
                "UserId",
                "NormalizedUserName",
                "CreatedUtc"
                ),
            collection: AuditTrailEvent.Collection
        );

        await CreateAttributionIndexesAsync();

        return 2;
    }

    // Edited in place (pre-release, docs/architecture.md › Rules that hold): a tenant that
    // enabled the audit trail before 2026-10-13 is reset to get these tables.
    private async Task CreateAttributionIndexesAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AuditTrailAttributionIndex>(table => table
            .Column<string>(nameof(AuditTrailAttributionIndex.EventId), column => column.WithLength(26))
            .Column<string>(nameof(AuditTrailAttributionIndex.Category), column => column.WithLength(64))
            .Column<string>(nameof(AuditTrailAttributionIndex.Name), column => column.WithLength(64))
            .Column<DateTime>(nameof(AuditTrailAttributionIndex.CreatedUtc), column => column.Nullable())
            .Column<string>(nameof(AuditTrailAttributionIndex.UserId), column => column.Nullable().WithLength(255))
            .Column<string>(nameof(AuditTrailAttributionIndex.ActorKind), column => column.Nullable().WithLength(16))
            .Column<string>(nameof(AuditTrailAttributionIndex.FunctionalUserId), column => column.Nullable().WithLength(255))
            .Column<string>(nameof(AuditTrailAttributionIndex.ImpersonatedUserId), column => column.Nullable().WithLength(255))
            .Column<bool>(nameof(AuditTrailAttributionIndex.IsImpersonated))
            .Column<string>(nameof(AuditTrailAttributionIndex.SessionId), column => column.Nullable().WithLength(26))
            .Column<string>(nameof(AuditTrailAttributionIndex.OrganizationId), column => column.Nullable().WithLength(26))
            .Column<string>(nameof(AuditTrailAttributionIndex.Side), column => column.Nullable().WithLength(16))
            .Column<string>(nameof(AuditTrailAttributionIndex.RecordId), column => column.Nullable().WithLength(255))
            .Column<string>(nameof(AuditTrailAttributionIndex.RecordType), column => column.Nullable().WithLength(255))
            .Column<string>(nameof(AuditTrailAttributionIndex.OwnerId), column => column.Nullable().WithLength(255)),
            collection: AuditTrailEvent.Collection);

        await SchemaBuilder.AlterIndexTableAsync<AuditTrailAttributionIndex>(table =>
        {
            table.CreateIndex("IDX_AuditTrailAttributionIndex_Actor", "DocumentId", "UserId", "CreatedUtc");
            table.CreateIndex("IDX_AuditTrailAttributionIndex_Functional", "DocumentId", "FunctionalUserId", "CreatedUtc");
            table.CreateIndex("IDX_AuditTrailAttributionIndex_Impersonated", "DocumentId", "ImpersonatedUserId", "SessionId", "CreatedUtc");
            table.CreateIndex("IDX_AuditTrailAttributionIndex_Organization", "DocumentId", "OrganizationId", "CreatedUtc");
            table.CreateIndex("IDX_AuditTrailAttributionIndex_Record", "DocumentId", "RecordId", "Category", "CreatedUtc");
        },
        collection: AuditTrailEvent.Collection);

        await SchemaBuilder.CreateMapIndexTableAsync<AuditTrailPartyIndex>(table => table
            .Column<string>(nameof(AuditTrailPartyIndex.EventId), column => column.WithLength(26))
            .Column<string>(nameof(AuditTrailPartyIndex.PartyId), column => column.WithLength(26))
            .Column<string>(nameof(AuditTrailPartyIndex.Category), column => column.WithLength(64))
            .Column<DateTime>(nameof(AuditTrailPartyIndex.CreatedUtc), column => column.Nullable()),
            collection: AuditTrailEvent.Collection);

        await SchemaBuilder.AlterIndexTableAsync<AuditTrailPartyIndex>(table => table
            .CreateIndex("IDX_AuditTrailPartyIndex_Party", "DocumentId", "PartyId", "Category", "CreatedUtc"),
            collection: AuditTrailEvent.Collection);
    }

    public async Task<int> UpdateFrom1Async()
    {
        // SQLite has no VARCHAR length limits and lacks ALTER INDEX support,
        // therefore we skip the following migration on that provider.
        if (session.Store.Configuration.SqlDialect.Name != DatabaseProviderValue.Sqlite)
        {
            await SchemaBuilder.AlterIndexTableAsync<AuditTrailEventIndex>(
                table =>
                    table.AlterColumn(
                        nameof(AuditTrailEventIndex.UserId),
                        column => column.WithType(typeof(string), 255)
                ),
                collection: AuditTrailEvent.Collection
            );
        }

        return 2;
    }
}
