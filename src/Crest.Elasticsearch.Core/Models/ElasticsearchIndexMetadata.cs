using System.Text.Json.Serialization;
using Crest.Elasticsearch.Core.Json;
using Crest.Elasticsearch.Models;

namespace Crest.Elasticsearch.Core.Models;

[JsonConverter(typeof(ElasticsearchIndexMetadataConverter))]
public sealed class ElasticsearchIndexMetadata
{
    public bool StoreSourceData { get; set; } = true;

    public string AnalyzerName { get; set; }

    [JsonConverter(typeof(ElasticsearchIndexMapConverter))]
    public ElasticsearchIndexMap IndexMappings { get; set; }

    public string GetAnalyzerName()
    {
        return string.IsNullOrEmpty(AnalyzerName)
            ? ElasticsearchConstants.DefaultAnalyzer
            : AnalyzerName;
    }

    public string GetQueryAnalyzerName()
    {
        return AnalyzerName ?? ElasticsearchConstants.DefaultAnalyzer;
    }
}
