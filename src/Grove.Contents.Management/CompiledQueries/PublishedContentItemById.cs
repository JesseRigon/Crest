using System.Linq.Expressions;
using Grove.Contents.Management.Records;
using YesSql;

namespace Grove.Contents.Management.CompiledQueries;

public class PublishedContentItemById : ICompiledQuery<ContentItem>
{
    public PublishedContentItemById(string contentItemId)
    {
        ContentItemId = contentItemId;
    }

    public string ContentItemId { get; set; }

    public Expression<Func<IQuery<ContentItem>, IQuery<ContentItem>>> Query()
    {
        return query => query
            .With<ContentItemIndex>()
            .Where(x => x.ContentItemId == ContentItemId && x.Published == true);
    }
}
