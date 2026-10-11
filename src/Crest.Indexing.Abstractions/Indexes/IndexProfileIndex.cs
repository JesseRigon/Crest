using YesSql.Indexes;

namespace Crest.Indexing.Indexes;

public sealed class IndexProfileIndex : MapIndex
{
    public string IndexProfileId { get; set; }

    public string Name { get; set; }

    public string IndexName { get; set; }

    public string ProviderName { get; set; }

    public string Type { get; set; }
}
