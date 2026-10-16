using Grove.Entities;
using Grove.Data.Queries;
using Grove.Data.Queries.Sql.Models;

namespace Grove.Data.Queries.Sql;

public sealed class SqlQueryHandler : QueryHandlerBase
{
    public override Task InitializingAsync(InitializingQueryContext context)
        => UpdateQueryAsync(context);

    public override Task UpdatingAsync(UpdatingQueryContext context)
        => UpdateQueryAsync(context);

    private static Task UpdateQueryAsync(DataQueryContextBase context)
    {
        if (context.Query.Source != SqlQuerySource.SourceName)
        {
            return Task.CompletedTask;
        }

        var template = context.Data[nameof(SqlQueryMetadata.Template)]?.GetValue<string>();
        var connection = context.Data[nameof(SqlQueryMetadata.Connection)];

        if (!string.IsNullOrEmpty(template) || connection is not null)
        {
            var metadata = context.Query.GetOrCreate<SqlQueryMetadata>();

            if (!string.IsNullOrEmpty(template))
            {
                metadata.Template = template;
            }

            if (connection is not null)
            {
                var key = connection.GetValue<string>()?.Trim();
                metadata.Connection = string.IsNullOrEmpty(key) ? null : key;
            }

            context.Query.Put(metadata);
        }

        return Task.CompletedTask;
    }
}
