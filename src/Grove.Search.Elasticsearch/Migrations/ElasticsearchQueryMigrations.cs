using Grove.Data.Migration;
using Grove.Data.Queries;
using Grove.Search.Elasticsearch.Services;

namespace Grove.Data.Queries.Sql.Migrations;

public sealed class ElasticsearchQueryMigrations : DataMigration
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
    public int Create()
    {
        QueriesDocumentMigrationHelper.Migrate(ElasticsearchQuerySource.SourceName);

        return 1;
    }
}
