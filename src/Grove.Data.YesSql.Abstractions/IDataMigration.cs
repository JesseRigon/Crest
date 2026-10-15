using Grove.Environment.Extensions;
using YesSql.Sql;

namespace Grove.Data.Migration;

/// <summary>
/// Represents a contract for a database migration.
/// </summary>
[FeatureTypeDiscovery(SingleFeatureOnly = true, SkipExtension = true)]
public interface IDataMigration
{
    /// <summary>
    /// Gets or sets the database schema builder.
    /// </summary>
    ISchemaBuilder SchemaBuilder { get; set; }
}
