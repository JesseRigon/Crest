using Grove.ContentManagement;
using YesSql;
using YesSql.Filters.Query.Services;

namespace Grove.Contents.Services;

public class ContentQueryContext : QueryExecutionContext<ContentItem>
{
    public ContentQueryContext(IServiceProvider serviceProvider, IQuery<ContentItem> query) : base(query)
    {
        ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }
}
