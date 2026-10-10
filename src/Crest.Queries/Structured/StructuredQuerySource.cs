#nullable enable
using System.Globalization;
using System.Text.Json.Nodes;
using Dapper;
using Crest.Access;
using Crest.ContentManagement;
using Crest.Data;
using Crest.Entities;
using YesSql;

namespace Crest.Queries.Structured;

/// <summary>
/// Runs a structured query: the step list compiled through the store's dialect, the caller's
/// scope conjoined for every table, values bound as parameters, pages written by the dialect.
/// </summary>
public sealed class StructuredQuerySource : IQuerySource, IQueryDescriber
{
    public const string SourceName = "Structured";

    private readonly IDbConnectionAccessor _dbConnectionAccessor;
    private readonly ISession _session;
    private readonly IIndexTableCatalog _catalog;
    private readonly ICallerContextAccessor? _callerAccessor;
    private readonly IScopeSetProvider? _scopeSetProvider;

    public StructuredQuerySource(
        IDbConnectionAccessor dbConnectionAccessor,
        ISession session,
        IIndexTableCatalog catalog,
        IServiceProvider serviceProvider)
    {
        _dbConnectionAccessor = dbConnectionAccessor;
        _session = session;
        _catalog = catalog;
        _callerAccessor = serviceProvider.GetService(typeof(ICallerContextAccessor)) as ICallerContextAccessor;
        _scopeSetProvider = serviceProvider.GetService(typeof(IScopeSetProvider)) as IScopeSetProvider;
    }

    public string Name => SourceName;

    public string Source => SourceName;

    public IReadOnlyList<QueryDescriptor> BuiltInQueries => [];

    public QueryDescriptor? Describe(Query query)
    {
        if (!query.TryGet<StructuredQueryMetadata>(out var metadata))
        {
            return null;
        }

        var plan = QueryPlan.Build(metadata.Definition, _catalog);

        return new QueryDescriptor(query.Name, query.Name, plan.Parameters, plan.OutputColumns, SourceName);
    }

    public async Task<IQueryResults> ExecuteQueryAsync(Query query, QueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        if (!query.TryGet<StructuredQueryMetadata>(out var metadata))
        {
            return QueryResults.Empty();
        }

        var cancellationToken = request.CancellationToken;
        var plan = QueryPlan.Build(metadata.Definition, _catalog);
        var scopes = await CallerScopes.RequireAsync(_callerAccessor, _scopeSetProvider, plan.From.Table.Name, cancellationToken);
        var configuration = _session.Store.Configuration;

        var statement = StructuredQueryCompiler.Compile(
            plan,
            configuration.SqlDialect,
            configuration.TablePrefix,
            configuration.Schema,
            scopes,
            request.Parameters,
            request.PageToken,
            request.PageSize);

        if (statement.IsEmpty)
        {
            return QueryResults.Empty(statement.Columns);
        }

        await using var connection = _dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(configuration.IsolationLevel, cancellationToken);

        var parameters = new DynamicParameters();

        foreach (var (name, value) in statement.Parameters)
        {
            parameters.Add(name, value);
        }

        long? total = null;

        if (statement.CountSql is not null)
        {
            total = await connection.ExecuteScalarAsync<long>(new CommandDefinition(statement.CountSql, parameters, transaction, cancellationToken: cancellationToken));
        }

        var rows = new List<JsonObject>();
        var columns = statement.Columns;

        await using (var reader = await connection.ExecuteReaderAsync(new CommandDefinition(statement.Sql, parameters, transaction, cancellationToken: cancellationToken)))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new JsonObject();

                for (var i = 0; i < columns.Count; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[columns[i].Name] = value is null ? null : JsonValue.Create(Typed(value, columns[i].Type));
                }

                rows.Add(row);
            }
        }

        string? nextPageToken = null;

        if (rows.Count == statement.PageSize && (total is null || statement.Offset + statement.PageSize < total))
        {
            nextPageToken = PageTokens.Encode(statement.Offset + statement.PageSize);
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

        if (statement.DocumentIdColumn is null)
        {
            throw new StructuredQueryException("A query that returns content items projects the From table's DocumentId.");
        }

        var documentIds = rows
            .Select(row => row[statement.DocumentIdColumn] is JsonValue value && long.TryParse(value.ToString(), CultureInfo.InvariantCulture, out var id) ? id : 0L)
            .Where(id => id != 0)
            .ToArray();

        return new QueryResults
        {
            Items = documentIds.Length > 0 ? await _session.GetAsync<ContentItem>(documentIds) : [],
            Columns = columns,
            Total = total,
            NextPageToken = nextPageToken,
        };
    }

    /// <summary>Brings a provider's raw value (SQLite's integers for booleans, strings for dates) to the column's CLR type.</summary>
    private static object Typed(object value, Type columnType)
    {
        var type = Nullable.GetUnderlyingType(columnType) ?? columnType;

        if (type.IsInstanceOfType(value) || type.IsEnum)
        {
            return value;
        }

        try
        {
            return type == typeof(DateTime)
                ? DateTime.Parse(value.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)
                : Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return value;
        }
    }
}
