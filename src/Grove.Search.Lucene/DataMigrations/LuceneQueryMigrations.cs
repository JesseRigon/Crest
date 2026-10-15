using Grove.Data.Migration;
using Grove.Data.Queries;
using Grove.Search.Lucene;

namespace Grove.Data.Queries.Sql.Migrations;

public sealed class LuceneQueryMigrations : DataMigration
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
    public int Create()
    {
        QueriesDocumentMigrationHelper.Migrate(LuceneQuerySource.SourceName);

        return 1;
    }
}
