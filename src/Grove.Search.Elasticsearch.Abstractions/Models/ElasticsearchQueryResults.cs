using Grove.Data.Queries;

namespace Grove.Search.Elasticsearch;

public class ElasticsearchQueryResults : IQueryResults
{
    public IEnumerable<object> Items { get; set; }
    public long Count { get; set; }
    public IReadOnlyList<QueryColumn> Columns => [];
    public long? Total => Count;
    public string NextPageToken => null;
}
