using Grove.Data.Migration;
using Grove.Data.Queries;

namespace Grove.Data.Queries.Sql.Migrations;

public sealed class SqlQueryMigrations : DataMigration
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
    public int Create()
    {
        QueriesDocumentMigrationHelper.Migrate(SqlQuerySource.SourceName);

        return 1;
    }
}
