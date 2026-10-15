using Elastic.Clients.Elasticsearch;
using Grove.Indexing.Models;

namespace Grove.Elasticsearch.Services;

public class ElasticsearchSearchContext
{
    public IndexProfile IndexProfile { get; }

    public SearchRequest SearchRequest { get; }

    public ElasticsearchSearchContext(IndexProfile indexProfile, SearchRequest searchRequest)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(searchRequest);

        IndexProfile = indexProfile;
        SearchRequest = searchRequest;
    }
}
