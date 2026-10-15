namespace Grove.Search.Elasticsearch.Models;

public enum ElasticsearchConnectionType
{
    SingleNodeConnectionPool,
    CloudConnectionPool,
    StaticConnectionPool,
    SniffingConnectionPool,
    StickyConnectionPool,
}
