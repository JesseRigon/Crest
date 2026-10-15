using Grove.Contents.Management;
using Grove.Contents.Taxonomies.Models;
using YesSql;

namespace Grove.Contents.Taxonomies;

public interface IContentsTaxonomyListFilter
{
    Task FilterAsync(IQuery<ContentItem> query, TermPart termPart);
}
