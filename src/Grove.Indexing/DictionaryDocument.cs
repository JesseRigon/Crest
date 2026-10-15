using Grove.Data.Documents;

namespace Grove.Indexing;

public sealed class DictionaryDocument<T> : Document
{
    public Dictionary<string, T> Records { get; init; } = [];
}
