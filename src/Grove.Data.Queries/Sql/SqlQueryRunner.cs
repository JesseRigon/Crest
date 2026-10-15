#nullable enable
using System.Diagnostics;
using Grove.Access;
using Grove.Entities;
using Grove.Data.Queries.Sql.Models;
using Grove.Data.Queries.Structured;
using YesSql;

namespace Grove.Data.Queries.Sql;

/// <summary>The outcome of an ad-hoc SQL run: the rows, or the messages that stopped it.</summary>
public sealed class SqlQueryRun
{
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>The statement as it runs: scoped and paged.</summary>
    public string? RawSql { get; init; }

    /// <summary>The parameters with their defaults applied, without the scope's own.</summary>
    public IReadOnlyDictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>();

    public IReadOnlyList<object> Rows { get; init; } = [];

    public IReadOnlyList<QueryColumn> Columns { get; init; } = [];

    public TimeSpan Elapsed { get; init; }

    /// <summary>The store's connection factory, so the runner can say which SQL dialect it speaks.</summary>
    public string? FactoryName { get; init; }

    public bool Succeeded => Errors.Count == 0;
}

/// <summary>
/// Runs an ad-hoc SQL template the way the admin's "Run SQL Query" screen does: validated as
/// SELECT-only, then through the query pipeline like a saved Sql query (scoped, paged, capped,
/// in a transaction never committed). The MVC screen and the Grove admin API share it.
/// </summary>
public sealed class SqlQueryRunner(IStore store, IQueryManager queryManager, IServiceProvider services)
{
    public string FactoryName => store.Configuration.ConnectionFactory.GetType().FullName ?? string.Empty;

    public async Task<SqlQueryRun> RunAsync(string sql, IDictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var errors = SqlParser.Validate(sql);

        if (errors.Count > 0)
        {
            return new SqlQueryRun { Errors = errors, FactoryName = FactoryName };
        }

        var values = new Dictionary<string, object>(parameters ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase);

        try
        {
            var scopes = await CallerScopes.RequireAsync(services, SqlQuerySource.SourceName, cancellationToken);
            var shown = new Dictionary<string, object>(values, StringComparer.OrdinalIgnoreCase);

            // Parsed here only to show the statement and the defaulted parameters.
            if (!SqlParser.TryParsePage(sql, store.Configuration.Schema, store.Configuration.SqlDialect, store.Configuration.TablePrefix, shown, scopes, out var pageQuery, out _, out var messages))
            {
                return new SqlQueryRun { Errors = messages.ToArray(), FactoryName = FactoryName, Elapsed = stopwatch.Elapsed };
            }

            shown.Remove(SqlParser.SkipParameter);
            shown.Remove(SqlParser.TakeParameter);
            var visible = shown
                .Where(pair => !pair.Key.StartsWith("__scope", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

            var query = new Query { Name = SqlQuerySource.SourceName, Source = SqlQuerySource.SourceName };
            query.Put(new SqlQueryMetadata { Template = sql });

            try
            {
                var results = await queryManager.ExecuteQueryAsync(query, new QueryRequest
                {
                    Parameters = values,
                    PageSize = StructuredQueryCompiler.MaxPageSize,
                    CancellationToken = cancellationToken,
                });

                return new SqlQueryRun
                {
                    RawSql = pageQuery,
                    Parameters = visible,
                    Rows = results.Items?.ToArray() ?? [],
                    Columns = results.Columns,
                    FactoryName = FactoryName,
                    Elapsed = stopwatch.Elapsed,
                };
            }
            catch (Exception exception) when (exception is not ScopeRefusedException and not OperationCanceledException)
            {
                return new SqlQueryRun
                {
                    RawSql = pageQuery,
                    Parameters = visible,
                    Errors = [$"An error occurred while executing the SQL query: {exception.Message}"],
                    FactoryName = FactoryName,
                    Elapsed = stopwatch.Elapsed,
                };
            }
        }
        catch (ScopeRefusedException exception)
        {
            return new SqlQueryRun { Errors = [$"The query was refused: {exception.Message}"], FactoryName = FactoryName, Elapsed = stopwatch.Elapsed };
        }
    }
}
