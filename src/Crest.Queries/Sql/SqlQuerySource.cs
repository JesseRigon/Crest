#nullable enable
using System.Globalization;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.Logging;
using Crest.Access;
using Crest.ContentManagement;
using Crest.ContentManagement.Records;
using Crest.Data;
using Crest.Entities;
using Crest.Queries.Sql.Models;
using Crest.Queries.Structured;
using YesSql;

namespace Crest.Queries.Sql;

/// <summary>
/// Runs an administrator-authored SQL template as written: values bind as parameters only,
/// the caller's scope is conjoined into every select before the statement runs, and the
/// statement is paged (and counted) as a derived table, like a structured query.
/// </summary>
public sealed class SqlQuerySource : IQuerySource
{
    public const string SourceName = "Sql";

    private readonly IDbConnectionAccessor _dbConnectionAccessor;
    private readonly ISession _session;
    private readonly ICallerContextAccessor? _callerAccessor;
    private readonly IScopeSetProvider? _scopeSetProvider;
    private readonly ILogger _logger;

    public SqlQuerySource(
        IDbConnectionAccessor dbConnectionAccessor,
        ISession session,
        IServiceProvider serviceProvider,
        ILogger<SqlQuerySource> logger)
    {
        _dbConnectionAccessor = dbConnectionAccessor;
        _session = session;
        _callerAccessor = serviceProvider.GetService(typeof(ICallerContextAccessor)) as ICallerContextAccessor;
        _scopeSetProvider = serviceProvider.GetService(typeof(IScopeSetProvider)) as IScopeSetProvider;
        _logger = logger;
    }

    public string Name
        => SourceName;

    public async Task<IQueryResults> ExecuteQueryAsync(Query query, QueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        var template = string.Empty;

        if (query.TryGet<SqlQueryMetadata>(out var metadata))
        {
            template = metadata.Template;
        }

        var cancellationToken = request.CancellationToken;
        var scopes = await CallerScopes.RequireAsync(_callerAccessor, _scopeSetProvider, query.Name ?? SourceName, cancellationToken);
        var configuration = _session.Store.Configuration;
        var parameters = new Dictionary<string, object>(request.Parameters, StringComparer.OrdinalIgnoreCase);

        if (!SqlParser.TryParsePage(
                template,
                configuration.Schema,
                configuration.SqlDialect,
                configuration.TablePrefix,
                parameters,
                scopes,
                out var pageQuery,
                out var countQuery,
                out var messages))
        {
            var errors = messages.ToArray();
            _logger.LogError("Couldn't parse SQL query: {Messages}", string.Join(' ', errors));

            throw new SqlQueryException(errors);
        }

        // Every run is paged like a structured query: the request's size, else the default, never past the cap.
        var pageSize = StructuredQueryCompiler.PageSize(request.PageSize);
        var offset = PageTokens.Decode(request.PageToken);
        parameters[SqlParser.SkipParameter] = offset;
        parameters[SqlParser.TakeParameter] = pageSize;

        await using var connection = _dbConnectionAccessor.CreateConnection();

        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(configuration.IsolationLevel, cancellationToken);

        var total = await connection.ExecuteScalarAsync<long>(new CommandDefinition(countQuery, parameters, transaction, cancellationToken: cancellationToken));

        QueryColumn[] columns;
        var rows = new List<JsonObject>();

        await using (var reader = await connection.ExecuteReaderAsync(new CommandDefinition(pageQuery, parameters, transaction, cancellationToken: cancellationToken)))
        {
            columns = new QueryColumn[reader.FieldCount];

            for (var i = 0; i < columns.Length; i++)
            {
                columns[i] = new QueryColumn(reader.GetName(i), reader.GetFieldType(i));
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new JsonObject();

                for (var i = 0; i < columns.Length; i++)
                {
                    row[columns[i].Name] = reader.IsDBNull(i) ? null : JsonValue.Create(reader.GetValue(i));
                }

                rows.Add(row);
            }
        }

        string? nextPageToken = null;

        if (rows.Count == pageSize && offset + pageSize < total)
        {
            nextPageToken = PageTokens.Encode(offset + pageSize);
        }

        if (!query.ReturnContentItems)
        {
            return new QueryResults
            {
                Items = rows,
                Columns = columns,
                Total = total,
                NextPageToken = nextPageToken,
            };
        }

        // Only a column named DocumentId points at documents; any other value (a literal, a
        // number from another table) could name a document the caller may not see.
        var column = columns.FirstOrDefault(candidate => string.Equals(candidate.Name, nameof(ContentItemIndex.DocumentId), StringComparison.OrdinalIgnoreCase))
            ?? throw new SqlQueryException(["A query that returns content items selects a DocumentId column."]);

        var documentIds = rows
            .Select(row => row[column.Name] is JsonValue value && long.TryParse(value.ToString(), CultureInfo.InvariantCulture, out var id) ? id : 0L)
            .Where(id => id != 0)
            .ToArray();

        return new QueryResults
        {
            Items = await ScopedContentItems.LoadAsync(_session, documentIds, scopes.Require(nameof(ContentItemIndex)), cancellationToken),
            Columns = columns,
            Total = total,
            NextPageToken = nextPageToken,
        };
    }
}
