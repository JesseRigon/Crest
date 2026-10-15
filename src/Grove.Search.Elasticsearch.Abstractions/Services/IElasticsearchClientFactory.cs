using Elastic.Clients.Elasticsearch;
using Grove.Search.Elasticsearch.Models;

namespace Grove.Search.Elasticsearch.Services;

public interface IElasticsearchClientFactory
{
    ElasticsearchClient Create(ElasticsearchConnectionOptions configuration);
}