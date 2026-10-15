using Grove.Data.Migration;
using Grove.Media.Scanning.Indexes;
using YesSql.Sql;

namespace Grove.Media.Scanning.Migrations;

public sealed class ScanJobMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ScanJobIndex>(table => table
            .Column<string>(nameof(ScanJobIndex.ScanId), column => column.WithLength(64))
            .Column<string>(nameof(ScanJobIndex.ContentId), column => column.WithLength(255))
            .Column<int>(nameof(ScanJobIndex.Version))
            .Column<int>(nameof(ScanJobIndex.Status))
            .Column<DateTime?>(nameof(ScanJobIndex.NextAttemptUtc), column => column.Nullable()));

        await SchemaBuilder.AlterIndexTableAsync<ScanJobIndex>(table => table
            .CreateIndex("IDX_ScanJobIndex_ScanId", "DocumentId", nameof(ScanJobIndex.ScanId)));

        await SchemaBuilder.AlterIndexTableAsync<ScanJobIndex>(table => table
            .CreateIndex("IDX_ScanJobIndex_Due", "DocumentId", nameof(ScanJobIndex.NextAttemptUtc)));

        await SchemaBuilder.AlterIndexTableAsync<ScanJobIndex>(table => table
            .CreateIndex("IDX_ScanJobIndex_Content", "DocumentId", nameof(ScanJobIndex.ContentId), nameof(ScanJobIndex.Version)));

        return 1;
    }
}
