namespace Crest.Elasticsearch.Models;

public enum ElasticsearchConnectionType
{
    SingleNodeConnectionPool,
    CloudConnectionPool,
    StaticConnectionPool,
    SniffingConnectionPool,
    StickyConnectionPool,
}
