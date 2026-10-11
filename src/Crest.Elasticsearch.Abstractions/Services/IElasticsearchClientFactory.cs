using Elastic.Clients.Elasticsearch;
using Crest.Elasticsearch.Models;

namespace Crest.Elasticsearch.Services;

public interface IElasticsearchClientFactory
{
    ElasticsearchClient Create(ElasticsearchConnectionOptions configuration);
}