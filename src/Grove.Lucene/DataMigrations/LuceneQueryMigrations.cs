using Grove.Data.Migration;
using Grove.Queries;
using Grove.Lucene;

namespace Grove.Queries.Sql.Migrations;

public sealed class LuceneQueryMigrations : DataMigration
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
    public int Create()
    {
        QueriesDocumentMigrationHelper.Migrate(LuceneQuerySource.SourceName);

        return 1;
    }
}
