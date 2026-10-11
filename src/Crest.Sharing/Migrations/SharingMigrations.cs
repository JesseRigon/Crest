using Crest.Data.Migration;
using Crest.Sharing.Indexes;
using YesSql.Sql;

namespace Crest.Sharing.Migrations;

public sealed class SharingMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ShareDocumentIndex>(table => table
            .Column<string>(nameof(ShareDocumentIndex.Kind), column => column.WithLength(64))
            .Column<string>(nameof(ShareDocumentIndex.ObjectId), column => column.WithLength(255)));

        await SchemaBuilder.AlterIndexTableAsync<ShareDocumentIndex>(table => table
            .CreateIndex("IDX_ShareDocumentIndex_Object", "DocumentId", nameof(ShareDocumentIndex.Kind), nameof(ShareDocumentIndex.ObjectId)));

        await SchemaBuilder.CreateMapIndexTableAsync<ShareEntryIndex>(table => table
            .Column<string>(nameof(ShareEntryIndex.Kind), column => column.WithLength(64))
            .Column<string>(nameof(ShareEntryIndex.ObjectId), column => column.WithLength(255))
            .Column<int>(nameof(ShareEntryIndex.PrincipalKind))
            .Column<string>(nameof(ShareEntryIndex.PrincipalId), column => column.WithLength(255))
            .Column<bool>(nameof(ShareEntryIndex.IsBlock)));

        await SchemaBuilder.AlterIndexTableAsync<ShareEntryIndex>(table => table
            .CreateIndex("IDX_ShareEntryIndex_Principal", "DocumentId", nameof(ShareEntryIndex.PrincipalKind), nameof(ShareEntryIndex.PrincipalId), nameof(ShareEntryIndex.IsBlock)));

        return 1;
    }
}
