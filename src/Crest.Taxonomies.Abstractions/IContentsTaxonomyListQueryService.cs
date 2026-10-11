using Crest.ContentManagement;
using Crest.Navigation;
using Crest.Taxonomies.Models;
using YesSql;

namespace Crest.Taxonomies;

public interface IContentsTaxonomyListQueryService
{
    Task<IQuery<ContentItem>> QueryAsync(TermPart termPart, Pager pager);
}
