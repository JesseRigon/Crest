using Crest.Tests.Apis.Context;

namespace Crest.Tests.Apis.Lucene;

public class LuceneContext : SiteContext
{
    static LuceneContext()
    {
    }

    public LuceneContext()
    {
        this.WithRecipe("luceneQueryTest");
    }
}
