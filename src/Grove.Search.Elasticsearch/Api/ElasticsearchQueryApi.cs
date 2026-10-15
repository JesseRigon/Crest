#nullable enable

using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fluid;
using Fluid.Values;
using Grove.Api;
using Grove.Search.Elasticsearch.Models;
using Grove.Search.Elasticsearch.Services;
using Grove.Data.Indexing;
using Grove.Display.Liquid;
using Grove.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Search.Elasticsearch.Api;

/// <summary>
/// The indexes page's Elasticsearch query tab: whether a connection is configured, an index's
/// information (settings and mappings), and running a query (a Liquid template rendered with the
/// JSON parameters) against one index. Replaces the MVC query, index information and "not
/// configured" screens. Saved Elasticsearch queries run through the query operations
/// (Grove.Data.Queries, <c>api/grove/queries/{name}/run</c>).
/// </summary>
public sealed class ElasticsearchQueryApi : ApiContributor
{
    public override string Name => "ElasticsearchQuery";

    public override string FeatureId => "Grove.Search.Elasticsearch";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("elasticsearch");
        routes.MapGet("status", ApiHandlers.Handle<ElasticsearchQueryHandlers>(handlers => handlers.StatusAsync()));
        routes.MapGet("indexes/{id}/info", (HttpContext __http, [FromRoute] string id) => ApiHandlers.For<ElasticsearchQueryHandlers>(__http).InfoAsync(id));
        routes.MapPost("indexes/{id}/query", (HttpContext __http, [FromRoute] string id, [FromBody] ElasticsearchQueryRequest request) => ApiHandlers.For<ElasticsearchQueryHandlers>(__http).QueryAsync(id, request));
    }
}

public sealed class ElasticsearchQueryHandlers(
    IAuthorizationService authorizationService,
    IIndexProfileStore indexProfileStore,
    ElasticsearchIndexManager indexManager,
    ElasticsearchQueryService queryService,
    ILiquidTemplateManager liquidTemplateManager,
    JavaScriptEncoder javaScriptEncoder,
    IOptions<TemplateOptions> templateOptions,
    IOptions<ElasticsearchConnectionOptions> connectionOptions,
    ILogger<ElasticsearchQueryHandlers> logger) : ApiHandlers
{
    public async Task<IResult> StatusAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, ElasticsearchPermissions.ManageElasticIndexes))
        {
            return Forbid();
        }

        return Ok(new ElasticsearchStatus(connectionOptions.Value.ConfigurationExists()));
    }

    public async Task<IResult> InfoAsync(string id)
    {
        if (!await authorizationService.AuthorizeAsync(User, ElasticsearchPermissions.ManageElasticIndexes))
        {
            return Forbid();
        }

        if (!connectionOptions.Value.ConfigurationExists())
        {
            return NotFound();
        }

        var index = await indexProfileStore.FindByIdAsync(id);
        if (index is null || index.ProviderName != ElasticsearchConstants.ProviderName)
        {
            return NotFound();
        }

        var info = await indexManager.GetIndexInfoAsync(index.IndexFullName);
        return Content(JNode.Parse(info)!.ToJsonString(JOptions.Indented), "application/json");
    }

    public async Task<IResult> QueryAsync(string id, [FromBody] ElasticsearchQueryRequest request)
    {
        if (!await authorizationService.AuthorizeAsync(User, ElasticsearchPermissions.ManageElasticIndexes))
        {
            return Forbid();
        }

        if (!connectionOptions.Value.ConfigurationExists())
        {
            return BadRequest();
        }

        var index = await indexProfileStore.FindByIdAsync(id);
        if (index is null || !await indexManager.ExistsAsync(index.IndexFullName))
        {
            return NotFound();
        }

        if (index.ProviderName != ElasticsearchConstants.ProviderName)
        {
            return BadRequest();
        }

        var stopwatch = Stopwatch.StartNew();
        long count = 0;
        var documents = new List<JsonObject>();
        try
        {
            var parameters = string.IsNullOrWhiteSpace(request.Parameters)
                ? []
                : JConvert.DeserializeObject<Dictionary<string, object>>(request.Parameters) ?? [];
            var tokenizedContent = await liquidTemplateManager.RenderStringAsync(
                request.Query!,
                javaScriptEncoder,
                parameters.Select(parameter => new KeyValuePair<string, FluidValue>(parameter.Key, FluidValue.Create(parameter.Value, templateOptions.Value))));

            if (!string.IsNullOrEmpty(tokenizedContent) && await queryService.SearchAsync(index, tokenizedContent) is { } results)
            {
                // The source documents, or the requested fields when the query names fields.
                var records = results.Fields?.Count > 0 ? results.Fields : results.TopDocs;
                documents.AddRange(records?.Select(record => record.Value) ?? []);
                count = results.Count;
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error while executing Elasticsearch query");
            return BadRequest(new ValidationErrors().Add("query", $"Invalid query: {exception.Message}"));
        }

        stopwatch.Stop();

        return Ok(new ElasticsearchQueryResult(count, (long)stopwatch.Elapsed.TotalMilliseconds, documents));
    }
}

public sealed record ElasticsearchStatus(bool IsConfigured);

/// <summary>A query: the Elasticsearch query JSON (a Liquid template) and its JSON parameters.</summary>
public sealed record ElasticsearchQueryRequest([property: NotEmpty] string? Query, string? Parameters);

public sealed record ElasticsearchQueryResult(long Count, long ElapsedMilliseconds, IReadOnlyList<JsonObject> Documents);
