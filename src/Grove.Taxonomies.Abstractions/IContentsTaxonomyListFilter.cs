using Grove.ContentManagement;
using Grove.Taxonomies.Models;
using YesSql;

namespace Grove.Taxonomies;

public interface IContentsTaxonomyListFilter
{
    Task FilterAsync(IQuery<ContentItem> query, TermPart termPart);
}
