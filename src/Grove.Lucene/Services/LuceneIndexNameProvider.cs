using Grove.Indexing;

namespace Grove.Lucene;

public sealed class LuceneIndexNameProvider : IIndexNameProvider
{
    public string GetFullIndexName(string name)
        => name;
}
