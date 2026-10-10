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
using YesSql;

namespace Crest.Queries.Sql;

/// <summary>
/// Runs an administrator-authored SQL template as written: values bind as parameters only,
/// the caller's scope is conjoined into every select before the statement runs.
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
        var parameters = new Dictionary<string, object>(request.Parameters);

        if (!SqlParser.TryParse(
                template,
                configuration.Schema,
                configuration.SqlDialect,
                configuration.TablePrefix,
                parameters,
                scopes,
                out var rawQuery,
                out var messages))
        {
            _logger.LogError("Couldn't parse SQL query: {Messages}", string.Join(' ', messages));

            return QueryResults.Empty();
        }

        await using var connection = _dbConnectionAccessor.CreateConnection();

        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(configuration.IsolationLevel, cancellationToken);
        await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(rawQuery, parameters, transaction, cancellationToken: cancellationToken));

        var columns = new QueryColumn[reader.FieldCount];

        for (var i = 0; i < columns.Length; i++)
        {
            columns[i] = new QueryColumn(reader.GetName(i), reader.GetFieldType(i));
        }

        var rows = new List<JsonObject>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new JsonObject();

            for (var i = 0; i < columns.Length; i++)
            {
                row[columns[i].Name] = reader.IsDBNull(i) ? null : JsonValue.Create(reader.GetValue(i));
            }

            rows.Add(row);
        }

        if (!query.ReturnContentItems)
        {
            return new QueryResults
            {
                Items = rows,
                Columns = columns,
            };
        }

        var column = columns.FirstOrDefault(candidate => string.Equals(candidate.Name, nameof(ContentItemIndex.DocumentId), StringComparison.OrdinalIgnoreCase))
            ?? columns.FirstOrDefault(candidate => candidate.Type == typeof(long) || candidate.Type == typeof(int))
            ?? columns.FirstOrDefault();

        if (column is null)
        {
            return QueryResults.Empty(columns);
        }

        var documentIds = rows
            .Select(row => row[column.Name] is JsonValue value && long.TryParse(value.ToString(), CultureInfo.InvariantCulture, out var id) ? id : 0L)
            .Where(id => id != 0)
            .ToArray();

        return new QueryResults
        {
            Items = documentIds.Length > 0 ? await _session.GetAsync<ContentItem>(documentIds) : [],
            Columns = columns,
        };
    }
}
