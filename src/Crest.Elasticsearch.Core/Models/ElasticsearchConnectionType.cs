namespace Crest.Elasticsearch.Core.Models;

public enum ElasticsearchConnectionType
{
    SingleNodeConnectionPool,
    CloudConnectionPool,
    StaticConnectionPool,
    SniffingConnectionPool,
    StickyConnectionPool,
}
