#nullable enable
namespace Crest.Queries;

/// <summary>
/// Contract for query data source.
/// </summary>
public interface IQuerySource
{
    /// <summary>
    /// Gets the name of query source.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes a query for the current caller, scoped before paging.
    /// </summary>
    /// <param name="query">The <see cref="Query"/> to be executed.</param>
    /// <param name="request">The parameters, page and cancellation of this run.</param>
    Task<IQueryResults> ExecuteQueryAsync(Query query, QueryRequest request);
}
