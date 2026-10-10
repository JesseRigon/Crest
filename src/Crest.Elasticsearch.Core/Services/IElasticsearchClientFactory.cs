using Elastic.Clients.Elasticsearch;
using Crest.Elasticsearch.Core.Models;

namespace Crest.Elasticsearch.Core.Services;

public interface IElasticsearchClientFactory
{
    ElasticsearchClient Create(ElasticsearchConnectionOptions configuration);
}