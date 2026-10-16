using Microsoft.Extensions.Logging;
using Grove.Contents.Management;
using Grove.Contents.Management.Records;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Data.Indexing;
using Grove.Contents.Taxonomies.Models;
using YesSql;

namespace Grove.Contents.Taxonomies;

public sealed class DefaultContentsTaxonomyListQueryService : IContentsTaxonomyListQueryService
{
    private readonly ISession _session;
    private readonly IEnumerable<IContentsTaxonomyListFilter> _contentTaxonomyListFilters;
    private readonly ILogger _logger;

    public DefaultContentsTaxonomyListQueryService(
        ISession session,
        IEnumerable<IContentsTaxonomyListFilter> contentTaxonomyListFilters,
        ILogger<DefaultContentsTaxonomyListQueryService> logger)
    {
        _session = session;
        _contentTaxonomyListFilters = contentTaxonomyListFilters;
        _logger = logger;
    }

    public async Task<IQuery<ContentItem>> QueryAsync(TermFieldSet termFieldSet, Pager pager)
    {
        ArgumentNullException.ThrowIfNull(termFieldSet);
        ArgumentNullException.ThrowIfNull(pager);

        // The items filed under the term: option list field selections of it (TaxonomyFields).
        var termId = termFieldSet.ContentItem.ContentItemId;
        IQuery<ContentItem> query = _session.Query<ContentItem>()
            .With<OptionListFieldIndex>(x => x.SelectedId == termId && x.Published);

        await _contentTaxonomyListFilters.InvokeAsync((filter, q, part) => filter.FilterAsync(q, part), query, termFieldSet, _logger);

        var currentPage = 1;

        if (pager.Page > 0)
        {
            currentPage = pager.Page;
        }

        return query
            .With<ContentItemIndex>()
            .ThenByDescending(x => x.CreatedUtc)
            .ThenBy(x => x.Id)
            .Skip((currentPage - 1) * pager.PageSize)
            .Take(pager.PageSize);
    }
}
