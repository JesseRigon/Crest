using Grove.Data.Indexing;

namespace Grove.Search.Elasticsearch.Models;

/// <summary>
/// Represents the indexing settings for a field set or a field.
/// </summary>
public class ElasticContentIndexSettings : IContentIndexSettings
{
    public bool Included { get; set; }

    public DocumentIndexOptions ToOptions()
    {
        var options = DocumentIndexOptions.None;

        return options;
    }
}
