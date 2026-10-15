using Elastic.Clients.Elasticsearch;
using Grove.Elasticsearch.Models;

namespace Grove.Elasticsearch.Services;

public interface IElasticsearchClientFactory
{
    ElasticsearchClient Create(ElasticsearchConnectionOptions configuration);
}