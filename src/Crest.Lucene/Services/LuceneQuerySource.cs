using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Options;
using Crest.ContentManagement;
using Crest.ContentManagement.Records;
using Crest.Entities;
using Crest.Indexing;
using Crest.Liquid;
using Crest.Lucene;
using Crest.Queries;
using Crest.Lucene.Models;
using Crest.Lucene.Services;
using Crest.Access;
using Crest.Queries.Structured;
using YesSql;
using YesSql.Services;

namespace Crest.Lucene;

/// <summary>
/// Runs a Lucene query template. Hits that are content items load through the caller's
/// <c>ContentItemIndex</c> scope; raw documents carry no scope and are served only to a caller
/// who may read every content item. Results are paged like a structured query.
/// </summary>
public sealed class LuceneQuerySource : IQuerySource
{
    public const string SourceName = LuceneConstants.ProviderName;

    private readonly ILuceneIndexStore _luceneIndexStore;
    private readonly IIndexProfileStore _indexStore;
    private readonly LuceneAnalyzerManager _luceneAnalyzerManager;
    private readonly ILuceneQueryService _queryService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly ISession _session;
    private readonly JavaScriptEncoder _javaScriptEncoder;
    private readonly TemplateOptions _templateOptions;
    private readonly ICallerContextAccessor _callerAccessor;
    private readonly IScopeSetProvider _scopeSetProvider;

    public LuceneQuerySource(
        ILuceneIndexStore luceneIndexStore,
        IIndexProfileStore indexStore,
        LuceneAnalyzerManager luceneAnalyzerManager,
        ILuceneQueryService queryService,
        ILiquidTemplateManager liquidTemplateManager,
        ISession session,
        JavaScriptEncoder javaScriptEncoder,
        IOptions<TemplateOptions> templateOptions,
        IServiceProvider serviceProvider)
    {
        _luceneIndexStore = luceneIndexStore;
        _indexStore = indexStore;
        _luceneAnalyzerManager = luceneAnalyzerManager;
        _queryService = queryService;
        _liquidTemplateManager = liquidTemplateManager;
        _session = session;
        _javaScriptEncoder = javaScriptEncoder;
        _templateOptions = templateOptions.Value;
        _callerAccessor = serviceProvider.GetService(typeof(ICallerContextAccessor)) as ICallerContextAccessor;
        _scopeSetProvider = serviceProvider.GetService(typeof(IScopeSetProvider)) as IScopeSetProvider;
    }

    public string Name
        => SourceName;

    public async Task<IQueryResults> ExecuteQueryAsync(Query query, QueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        var cancellationToken = request.CancellationToken;
        var scopes = await CallerScopes.RequireAsync(_callerAccessor, _scopeSetProvider, query.Name ?? SourceName, cancellationToken);
        var rule = scopes.Require(nameof(ContentItemIndex));

        if (!query.ReturnContentItems && rule.Kind != ScopeKind.All)
        {
            throw new ScopeRefusedException($"{SourceName} documents");
        }

        var pageSize = StructuredQueryCompiler.PageSize(request.PageSize);
        var offset = PageTokens.Decode(request.PageToken);
        var parameters = request.Parameters;

        if (!query.TryGet<LuceneQueryMetadata>(out var metadata) || string.IsNullOrEmpty(metadata.Index))
        {
            return QueryResults.Empty();
        }

        var index = await _indexStore.FindByNameAsync(metadata.Index);

        if (index is null || index.ProviderName != LuceneConstants.ProviderName)
        {
            return QueryResults.Empty();
        }

        IReadOnlyList<object> items = [];

        await _luceneIndexStore.SearchAsync(index, async searcher =>
        {
            var tokenizedContent = await _liquidTemplateManager.RenderStringAsync(metadata.Template, _javaScriptEncoder, parameters.Select(x => new KeyValuePair<string, FluidValue>(x.Key, FluidValue.Create(x.Value, _templateOptions))));

            var parameterizedQuery = JsonNode.Parse(tokenizedContent, JOptions.Node, JOptions.Document).AsObject();

            var analyzerName = LuceneConstants.DefaultAnalyzer;

            if (index.TryGet<LuceneIndexMetadata>(out var indexMetadata) &&
                !string.IsNullOrEmpty(indexMetadata.AnalyzerName))
            {
                analyzerName = indexMetadata.AnalyzerName;
            }

            var analyzer = _luceneAnalyzerManager.CreateAnalyzer(analyzerName);

            var defaultVersion = LuceneConstants.DefaultVersion;
            if (index.TryGet<LuceneIndexDefaultQueryMetadata>(out var queryMetadata))
            {
                defaultVersion = queryMetadata.DefaultVersion;
            }

            var context = new LuceneQueryContext(searcher, defaultVersion, analyzer);
            var docs = await _queryService.SearchAsync(context, parameterizedQuery);

            if (query.ReturnContentItems)
            {
                // Load the hits' content item versions through the caller's scope, in hit order.
                var indexedContentItemVersionIds = docs.TopDocs.ScoreDocs
                    .Select(x => searcher.Doc(x.Doc).Get("ContentItemVersionId"))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToArray();

                items = await ScopedContentItems.LoadVersionsAsync(_session, indexedContentItemVersionIds, rule, cancellationToken);
            }
            else
            {
                var results = new List<JsonObject>();

                foreach (var document in docs.TopDocs.ScoreDocs.Select(hit => searcher.Doc(hit.Doc)))
                {
                    results.Add(new JsonObject(document.Select(x =>
                        KeyValuePair.Create(x.Name, (JsonNode)JsonValue.Create(x.GetStringValue())))));
                }

                items = results;
            }
        });

        return new QueryResults
        {
            Items = items.Skip(offset).Take(pageSize).ToArray(),
            Total = items.Count,
            NextPageToken = offset + pageSize < items.Count ? PageTokens.Encode(offset + pageSize) : null,
        };
    }
}
