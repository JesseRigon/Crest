#nullable enable
namespace Crest.Queries;

/// <summary>The results a source hands back: rows, columns, a total when known, a page token.</summary>
public sealed class QueryResults : IQueryResults
{
    public IEnumerable<object> Items { get; set; } = [];

    public IReadOnlyList<QueryColumn> Columns { get; init; } = [];

    public long? Total { get; init; }

    public string? NextPageToken { get; init; }

    public static QueryResults Empty(IReadOnlyList<QueryColumn>? columns = null) => new()
    {
        Columns = columns ?? [],
        Total = 0,
    };
}
