using Microsoft.Extensions.Logging;
using Crest.ContentManagement;
using Crest.ContentManagement.Records;
using Crest.Modules;
using Crest.Navigation;
using Crest.Taxonomies.Indexing;
using Crest.Taxonomies.Models;
using YesSql;

namespace Crest.Taxonomies.Core;

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

    public async Task<IQuery<ContentItem>> QueryAsync(TermPart termPart, Pager pager)
    {
        ArgumentNullException.ThrowIfNull(termPart);
        ArgumentNullException.ThrowIfNull(pager);

        IQuery<ContentItem> query = _session.Query<ContentItem>()
            .With<TaxonomyIndex>(x => x.TermContentItemId == termPart.ContentItem.ContentItemId);

        await _contentTaxonomyListFilters.InvokeAsync((filter, q, part) => filter.FilterAsync(q, part), query, termPart, _logger);

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
