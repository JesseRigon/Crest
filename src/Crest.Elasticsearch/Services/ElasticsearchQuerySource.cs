using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Options;
using Crest.ContentManagement;
using Crest.ContentManagement.Records;
using Crest.Entities;
using Crest.Indexing;
using Crest.Liquid;
using Crest.Queries;
using Crest.Elasticsearch.Models;
using Crest.Access;
using Crest.Queries.Structured;
using YesSql;
using YesSql.Services;

namespace Crest.Elasticsearch.Services;

/// <summary>
/// Runs an Elasticsearch query template. Hits that are content items load through the
/// caller's <c>ContentItemIndex</c> scope; raw documents carry no scope and are served only to
/// a caller who may read every content item. Results are paged like a structured query.
/// </summary>
public sealed class ElasticsearchQuerySource : IQuerySource
{
    public const string SourceName = ElasticsearchConstants.ProviderName;

    private readonly ElasticsearchQueryService _queryService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly ISession _session;
    private readonly JavaScriptEncoder _javaScriptEncoder;
    private readonly TemplateOptions _templateOptions;
    private readonly IServiceProvider _services;

    public ElasticsearchQuerySource(
        ElasticsearchQueryService queryService,
        ILiquidTemplateManager liquidTemplateManager,
        IIndexProfileStore indexProfileStore,
        ISession session,
        JavaScriptEncoder javaScriptEncoder,
        IOptions<TemplateOptions> templateOptions,
        IServiceProvider serviceProvider)
    {
        _queryService = queryService;
        _liquidTemplateManager = liquidTemplateManager;
        _indexProfileStore = indexProfileStore;
        _session = session;
        _javaScriptEncoder = javaScriptEncoder;
        _templateOptions = templateOptions.Value;
        _services = serviceProvider;
    }

    public string Name
        => SourceName;

    public async Task<IQueryResults> ExecuteQueryAsync(Query query, QueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        var cancellationToken = request.CancellationToken;
        var scopes = await CallerScopes.RequireAsync(_services, query.Name ?? SourceName, cancellationToken);
        var rule = scopes.Require(nameof(ContentItemIndex));

        if (!query.ReturnContentItems && rule.Kind != ScopeKind.All)
        {
            throw new ScopeRefusedException($"{SourceName} documents");
        }

        var pageSize = StructuredQueryCompiler.PageSize(request.PageSize);
        var offset = PageTokens.Decode(request.PageToken);
        var parameters = request.Parameters;

        if (!query.TryGet<ElasticsearchQueryMetadata>(out var metadata) || string.IsNullOrEmpty(metadata.Index))
        {
            return QueryResults.Empty();
        }

        var tokenizedContent = await _liquidTemplateManager.RenderStringAsync(metadata.Template, _javaScriptEncoder, parameters.Select(x => new KeyValuePair<string, FluidValue>(x.Key, FluidValue.Create(x.Value, _templateOptions))));

        var index = await _indexProfileStore.FindByNameAsync(metadata.Index);

        if (index is null || index.ProviderName != ElasticsearchConstants.ProviderName)
        {
            return QueryResults.Empty();
        }

        var docs = await _queryService.SearchAsync(index, tokenizedContent);

        if (docs.Count == 0 || docs.TopDocs == null || docs.TopDocs.Count == 0)
        {
            return QueryResults.Empty();
        }

        IReadOnlyList<object> items;

        if (query.ReturnContentItems)
        {
            // Load the hits' content item versions through the caller's scope, in hit order.
            var indexedContentItemVersionIds = new List<string>();

            foreach (var topDoc in docs.TopDocs.Where(x => x != null))
            {
                if (topDoc.Value.TryGetPropertyValue(nameof(ContentItem.ContentItemVersionId), out var versionId) && versionId is not null)
                {
                    indexedContentItemVersionIds.Add(versionId.GetValue<string>());
                }
            }

            items = await ScopedContentItems.LoadVersionsAsync(_session, indexedContentItemVersionIds, rule, cancellationToken);
        }
        else
        {
            var results = new List<JsonObject>();

            foreach (var document in docs.TopDocs)
            {
                results.Add(new JsonObject(document.Value
                    .Where(x => x.Value is not null)
                    .Select(x => KeyValuePair.Create(x.Key, x.Value.DeepClone()))));
            }

            items = results;
        }

        return new QueryResults
        {
            Items = items.Skip(offset).Take(pageSize).ToArray(),
            Total = items.Count,
            NextPageToken = offset + pageSize < items.Count ? PageTokens.Encode(offset + pageSize) : null,
        };
    }
}
