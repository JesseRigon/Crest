using Crest.Data.Migration;
using Crest.Queries.Core;
using Crest.Elasticsearch.Core.Services;

namespace Crest.Queries.Sql.Migrations;

public sealed class ElasticsearchQueryMigrations : DataMigration
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
    public int Create()
    {
        QueriesDocumentMigrationHelper.Migrate(ElasticsearchQuerySource.SourceName);

        return 1;
    }
}
