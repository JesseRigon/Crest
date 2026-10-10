#nullable enable
namespace Crest.Queries;

/// <summary>
/// One run of a query: the bound parameters, the page asked for and the cancellation. The
/// caller and their scope are not here; a source reads them from the access machinery.
/// </summary>
public sealed class QueryRequest
{
    public IDictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>();

    /// <summary>The token a previous result handed back as <see cref="IQueryResults.NextPageToken"/>; null for the first page.</summary>
    public string? PageToken { get; init; }

    /// <summary>Overrides the query's page size when set; never above the source's cap.</summary>
    public int? PageSize { get; init; }

    public CancellationToken CancellationToken { get; init; }

    public static QueryRequest Of(IDictionary<string, object>? parameters, CancellationToken cancellationToken = default) => new()
    {
        Parameters = parameters ?? new Dictionary<string, object>(),
        CancellationToken = cancellationToken,
    };
}
