using Grove.Entities;
using Grove.Data.Queries;
using Grove.Search.Lucene.Models;
using Grove.Tests.Apis.Context;

namespace Grove.Tests.Apis.Lucene;

public class LuceneContext : SiteContext
{
    static LuceneContext()
    {
    }

    public LuceneContext()
    {
        this.WithRecipe("luceneQueryTest");
    }

    /// <summary>
    /// Runs an ad-hoc Lucene query template against an index, returning content items (what the
    /// retired <c>api/lucene/content</c> did, in-process).
    /// </summary>
    public async Task<LuceneQueryResults> QueryContentAsync(string indexName, string query)
    {
        LuceneQueryResults results = null;
        await UsingTenantScopeAsync(async scope =>
        {
            var queryManager = scope.ServiceProvider.GetRequiredService<IQueryManager>();
            var luceneQuery = await queryManager.NewAsync(LuceneQuerySource.SourceName);
            luceneQuery.ReturnContentItems = true;
            luceneQuery.Put(new LuceneQueryMetadata
            {
                Index = indexName,
                Template = query,
            });

            results = (LuceneQueryResults)await queryManager.ExecuteQueryAsync(luceneQuery, QueryRequest.Of(null));
        });

        return results;
    }
}
