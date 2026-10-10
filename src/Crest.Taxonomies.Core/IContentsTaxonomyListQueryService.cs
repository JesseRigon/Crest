using Crest.ContentManagement;
using Crest.Navigation;
using Crest.Taxonomies.Models;
using YesSql;

namespace Crest.Taxonomies.Core;

public interface IContentsTaxonomyListQueryService
{
    Task<IQuery<ContentItem>> QueryAsync(TermPart termPart, Pager pager);
}
