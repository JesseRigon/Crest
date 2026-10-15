#nullable enable

using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fluid;
using Fluid.Values;
using Grove.Api;
using Grove.Entities;
using Grove.Indexing;
using Grove.Liquid;
using Grove.Lucene.Models;
using Grove.Lucene.Services;
using Grove.Search.Lucene;
using Grove.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Lucene.Api;

/// <summary>
/// Runs a Lucene query against one Lucene index for the indexes page's query tab: the query is a
/// Liquid template rendered with the JSON parameters, and the answer is the hits' stored fields.
/// Replaces the MVC Lucene query screen; the search APIs (<c>api/lucene</c>) stay for callers.
/// </summary>
public sealed class LuceneQueryApi : ApiContributor
{
    public override string Name => "LuceneQuery";

    public override string FeatureId => "Grove.Lucene";

    public override void Map(IEndpointRouteBuilder group) =>
        group.MapPost("lucene/indexes/{id}/query", (HttpContext __http, [FromRoute] string id, [FromBody] IndexQueryRequest request) => ApiHandlers.For<LuceneQueryHandlers>(__http).QueryAsync(id, request));
}

public sealed class LuceneQueryHandlers(
    IAuthorizationService authorizationService,
    IIndexProfileStore indexProfileStore,
    LuceneIndexManager indexManager,
    ILuceneIndexStore indexStore,
    LuceneAnalyzerManager analyzerManager,
    ILuceneQueryService queryService,
    ILiquidTemplateManager liquidTemplateManager,
    JavaScriptEncoder javaScriptEncoder,
    IOptions<TemplateOptions> templateOptions,
    ILogger<LuceneQueryHandlers> logger) : ApiHandlers
{
    public async Task<IResult> QueryAsync(string id, [FromBody] IndexQueryRequest request)
    {
        if (!await authorizationService.AuthorizeAsync(User, LuceneSearchPermissions.ManageLuceneIndexes))
        {
            return Forbid();
        }

        var index = await indexProfileStore.FindByIdAsync(id);
        if (index is null || !await indexManager.ExistsAsync(index.IndexFullName))
        {
            return NotFound();
        }

        if (index.ProviderName != LuceneConstants.ProviderName)
        {
            return BadRequest();
        }

        var stopwatch = Stopwatch.StartNew();
        var documents = new List<JsonObject>();
        var count = 0;
        try
        {
            var parameters = string.IsNullOrWhiteSpace(request.Parameters)
                ? []
                : JConvert.DeserializeObject<Dictionary<string, object>>(request.Parameters) ?? [];

            await indexStore.SearchAsync(index, async searcher =>
            {
                var defaultVersion = index.TryGet<LuceneIndexDefaultQueryMetadata>(out var queryMetadata) ? queryMetadata.DefaultVersion : LuceneConstants.DefaultVersion;
                var metadata = index.TryGet<LuceneIndexMetadata>(out var stored) ? stored : null;
                var context = new LuceneQueryContext(searcher, defaultVersion, analyzerManager.CreateAnalyzer(metadata?.AnalyzerName));

                var tokenizedContent = await liquidTemplateManager.RenderStringAsync(
                    request.Query!,
                    javaScriptEncoder,
                    parameters.Select(parameter => new KeyValuePair<string, FluidValue>(parameter.Key, FluidValue.Create(parameter.Value, templateOptions.Value))));

                if (!string.IsNullOrEmpty(tokenizedContent))
                {
                    var parameterizedQuery = JsonNode.Parse(tokenizedContent, JOptions.Node, JOptions.Document)!.AsObject();
                    var topDocs = await queryService.SearchAsync(context, parameterizedQuery);
                    if (topDocs != null)
                    {
                        foreach (var hit in topDocs.TopDocs.ScoreDocs)
                        {
                            var document = searcher.Doc(hit.Doc);
                            var fields = new JsonObject();
                            foreach (var field in document.Fields)
                            {
                                fields[field.Name] = field.GetStringValue();
                            }

                            documents.Add(fields);
                        }

                        count = topDocs.Count;
                    }
                }
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error while executing Lucene query");
            return BadRequest(new ValidationErrors().Add("query", $"Invalid query: {exception.Message}"));
        }

        stopwatch.Stop();

        return Ok(new IndexQueryResult(count, (long)stopwatch.Elapsed.TotalMilliseconds, documents));
    }
}

/// <summary>A query: the provider's query JSON (a Liquid template) and its JSON parameters.</summary>
public sealed record IndexQueryRequest([property: NotEmpty] string? Query, string? Parameters);

/// <summary>The hits' count, the time taken, and the hits (their stored fields, or the source document).</summary>
public sealed record IndexQueryResult(long Count, long ElapsedMilliseconds, IReadOnlyList<JsonObject> Documents);
