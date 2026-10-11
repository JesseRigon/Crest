using Crest.Indexing;

namespace Crest.Lucene;

public sealed class LuceneIndexNameProvider : IIndexNameProvider
{
    public string GetFullIndexName(string name)
        => name;
}
