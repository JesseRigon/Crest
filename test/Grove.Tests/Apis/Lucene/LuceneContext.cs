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
}
