using Crest.Queries;

namespace Crest.Search.Lucene;

public class LuceneQueryResults : IQueryResults
{
    public IEnumerable<object> Items { get; set; }
    public int Count { get; set; }
}
