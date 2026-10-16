#nullable enable

using Grove.Data.Connectors;
using Grove.Data.Queries.Structured;
using Grove.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Data.Queries.Connections;

/// <summary>
/// Runs one operation of a connection (a REST, GraphQL or SOAP connection, declared or
/// defined) through the connector engine and maps the answer into typed, paged rows: the
/// operation's own paging when it has one (the engine's next-page position is the page
/// token), else a single page. The external system applies its own permissions to the
/// credential the call runs with (docs/queries.md › Connections).
/// </summary>
public sealed class ConnectionQuerySource(IServiceProvider services) : IQuerySource
{
    public const string SourceName = "Connection";

    public string Name => SourceName;

    public async Task<IQueryResults> ExecuteQueryAsync(Query query, QueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        if (!query.TryGet<ConnectionQueryMetadata>(out var metadata) || string.IsNullOrWhiteSpace(metadata.Connection) || string.IsNullOrWhiteSpace(metadata.Operation))
        {
            throw new ConnectionQueryException("A connection query names a connection and an operation.");
        }

        var engine = services.GetService<IConnectorEngine>()
            ?? throw new ConnectionQueryException("The connector engine is not enabled.");
        var cancellationToken = request.CancellationToken;
        var arguments = ConnectionQueryRunner.Arguments(metadata.Arguments, request.Parameters);

        // The operation's page size parameter carries the run's page size, unless fixed.
        if (services.GetService<IConnectionRegistry>() is { } registry
            && await registry.FindOperationAsync(metadata.Connection, metadata.Operation, cancellationToken) is { Paging.SizeParameter: { Length: > 0 } sizeParameter }
            && !arguments.ContainsKey(sizeParameter))
        {
            arguments[sizeParameter] = StructuredQueryCompiler.PageSize(request.PageSize);
        }

        var (caller, scope) = await ConnectionQueryRunner.ResolveCallerAsync(services, cancellationToken);
        var result = await ConnectionQueryRunner.ExecuteAsync(engine, new ConnectorCall
        {
            ConnectionKey = metadata.Connection,
            Caller = caller,
            Scope = scope,
            OperationId = metadata.Operation,
            Arguments = arguments,
            Page = request.PageToken,
        }, cancellationToken);

        var rows = ConnectionQueryRunner.Rows(result.Value, metadata.RowsPath);

        return new QueryResults
        {
            Items = rows,
            Columns = ConnectionQueryRunner.Columns(rows),
            Total = result.NextPage is null && request.PageToken is null ? rows.Count : null,
            NextPageToken = result.NextPage,
        };
    }
}

/// <summary>Reads the connection, operation, arguments and rows path from the data a query is created or updated with.</summary>
public sealed class ConnectionQueryHandler : QueryHandlerBase
{
    public override Task InitializingAsync(InitializingQueryContext context) => UpdateQueryAsync(context);

    public override Task UpdatingAsync(UpdatingQueryContext context) => UpdateQueryAsync(context);

    private static Task UpdateQueryAsync(DataQueryContextBase context)
    {
        if (context.Query.Source != ConnectionQuerySource.SourceName || context.Data is not System.Text.Json.Nodes.JsonObject data)
        {
            return Task.CompletedTask;
        }

        var metadata = context.Query.GetOrCreate<ConnectionQueryMetadata>();
        metadata.Connection = data[nameof(ConnectionQueryMetadata.Connection)]?.GetValue<string>() ?? metadata.Connection;
        metadata.Operation = data[nameof(ConnectionQueryMetadata.Operation)]?.GetValue<string>() ?? metadata.Operation;
        metadata.RowsPath = data[nameof(ConnectionQueryMetadata.RowsPath)]?.GetValue<string>() ?? metadata.RowsPath;

        if (data[nameof(ConnectionQueryMetadata.Arguments)] is System.Text.Json.Nodes.JsonObject arguments)
        {
            metadata.Arguments = (System.Text.Json.Nodes.JsonObject)arguments.DeepClone();
        }

        context.Query.Put(metadata);

        return Task.CompletedTask;
    }
}
