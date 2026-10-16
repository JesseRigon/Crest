namespace Grove.Data.Queries.Sql.Models;

public sealed class SqlQueryMetadata
{
    public string Template { get; set; }

    /// <summary>
    /// The SQL connection the template runs against (an external database on the Connections
    /// screen); empty for the tenant's own database, where the table prefix and the caller's
    /// scope apply.
    /// </summary>
    public string Connection { get; set; }
}
