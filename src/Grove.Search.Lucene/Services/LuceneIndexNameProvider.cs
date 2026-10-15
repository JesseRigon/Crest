using Grove.Data.Indexing;

namespace Grove.Search.Lucene;

public sealed class LuceneIndexNameProvider : IIndexNameProvider
{
    public string GetFullIndexName(string name)
        => name;
}
