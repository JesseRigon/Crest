using Crest.Data.Documents;

namespace Crest.Indexing;

public sealed class DictionaryDocument<T> : Document
{
    public Dictionary<string, T> Records { get; init; } = [];
}
