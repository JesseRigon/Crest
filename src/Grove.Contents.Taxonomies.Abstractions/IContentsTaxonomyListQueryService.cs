using Grove.Contents.Management;
using Grove.Display.Navigation;
using Grove.Contents.Taxonomies.Models;
using YesSql;

namespace Grove.Contents.Taxonomies;

public interface IContentsTaxonomyListQueryService
{
    Task<IQuery<ContentItem>> QueryAsync(TermFieldSet termFieldSet, Pager pager);
}
