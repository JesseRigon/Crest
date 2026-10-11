using Crest.ContentManagement;
using Crest.Taxonomies.Models;
using YesSql;

namespace Crest.Taxonomies;

public interface IContentsTaxonomyListFilter
{
    Task FilterAsync(IQuery<ContentItem> query, TermPart termPart);
}
