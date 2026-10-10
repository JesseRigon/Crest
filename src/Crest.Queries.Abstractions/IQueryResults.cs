#nullable enable
namespace Crest.Queries;

/// <summary>
/// One page of a query's rows, with the columns they carry.
/// </summary>
public interface IQueryResults
{
    /// <summary>The rows of this page: content items when the query returns them, otherwise row objects.</summary>
    IEnumerable<object> Items { get; set; }

    /// <summary>The columns of the rows, with their CLR types; empty when the source cannot tell.</summary>
    IReadOnlyList<QueryColumn> Columns { get; }

    /// <summary>The number of rows across all pages, when the source can give it cheaply.</summary>
    long? Total { get; }

    /// <summary>The token for the next page; null when this is the last page or the source does not page.</summary>
    string? NextPageToken { get; }
}
