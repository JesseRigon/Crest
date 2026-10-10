#nullable enable
namespace Crest.Queries;

public static class QueryExecutionExtensions
{
    /// <summary>Runs a query with bound parameters, first page, no page size override.</summary>
    public static Task<IQueryResults> ExecuteQueryAsync(this IQueryManager queryManager, Query query, IDictionary<string, object>? parameters, CancellationToken cancellationToken = default)
        => queryManager.ExecuteQueryAsync(query, QueryRequest.Of(parameters, cancellationToken));
}
