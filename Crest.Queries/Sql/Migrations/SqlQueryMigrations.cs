using Crest.Data.Migration;
using Crest.Queries.Core;

namespace Crest.Queries.Sql.Migrations;

public sealed class SqlQueryMigrations : DataMigration
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
    public int Create()
    {
        QueriesDocumentMigrationHelper.Migrate(SqlQuerySource.SourceName);

        return 1;
    }
}
