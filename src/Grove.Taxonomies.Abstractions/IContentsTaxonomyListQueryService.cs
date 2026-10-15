using Grove.ContentManagement;
using Grove.Navigation;
using Grove.Taxonomies.Models;
using YesSql;

namespace Grove.Taxonomies;

public interface IContentsTaxonomyListQueryService
{
    Task<IQuery<ContentItem>> QueryAsync(TermPart termPart, Pager pager);
}
