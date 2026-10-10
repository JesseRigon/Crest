using Crest.Queries;

namespace Crest.Search.Lucene;

public class LuceneQueryResults : IQueryResults
{
    public IEnumerable<object> Items { get; set; }
    public int Count { get; set; }
    public IReadOnlyList<QueryColumn> Columns => [];
    public long? Total => Count;
    public string NextPageToken => null;
}
