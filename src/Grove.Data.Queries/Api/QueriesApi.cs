#nullable enable

using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Api;
using Microsoft.AspNetCore.Authorization;
using Grove.Validation;
using Grove.Data.Queries.Sql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Data.Queries.Api;

/// <summary>Saved queries and the admin's SQL runner (<c>api/grove/queries</c>).</summary>
public sealed class QueriesApi : ApiContributor
{
    public override string Name => "Queries";

    public override string FeatureId => "Grove.Data.Queries";

    public override void Map(IEndpointRouteBuilder group)
    {
        var queries = group.MapGroup("queries");
        queries.MapGet("", ListAsync);
        queries.MapPost("", CreateAsync);
        queries.MapPut("{name}", UpdateAsync);
        queries.MapDelete("{name}", DeleteAsync);
        queries.MapPost("delete", DeleteManyAsync);
        queries.MapPost("sql", RunSqlAsync);

        // Running a saved query: anonymous callers allowed, the query's own permission decides
        // (a refusal answers 404, never revealing the query); parameters come from the
        // `parameters` query value or, on POST, the JSON body.
        queries.MapGet("{name}/run", RunAsync).WithName("ApiExecuteQueryGet").AllowAnonymous().SkipApiAntiforgery();
        queries.MapPost("{name}/run", RunAsync).WithName("ApiExecuteQueryPost").AllowAnonymous().SkipApiAntiforgery();
    }

    private static async Task<IResult> RunAsync(
        HttpContext http,
        [FromRoute] string name,
        [FromQuery] string? parameters,
        [FromQuery] string? pageToken,
        [FromQuery] int? pageSize,
        [FromServices] IAuthorizationService authorization,
        [FromServices] IQueryManager queryManager)
    {
        var query = await queryManager.GetQueryAsync(name);
        if (query is null || !await authorization.AuthorizeAsync(http.User, Permissions.CreatePermissionForQuery(query.Name)))
        {
            return TypedResults.NotFound();
        }

        if (HttpMethods.IsPost(http.Request.Method) && string.IsNullOrEmpty(parameters))
        {
            using var reader = new StreamReader(http.Request.Body, Encoding.UTF8);
            parameters = await reader.ReadToEndAsync(http.RequestAborted);
        }

        var request = new QueryRequest
        {
            Parameters = string.IsNullOrWhiteSpace(parameters)
                ? new Dictionary<string, object>()
                : JConvert.DeserializeObject<Dictionary<string, object>>(parameters) ?? new Dictionary<string, object>(),
            PageToken = pageToken,
            PageSize = pageSize,
            CancellationToken = http.RequestAborted,
        };

        try
        {
            var result = await queryManager.ExecuteQueryAsync(query, request);
            return TypedResults.Ok(new QueryRunResult(
                result.Items,
                result.Columns.Select(column => new QueryRunColumn(column.Name, column.Type.Name)).ToArray(),
                result.Total,
                result.NextPageToken));
        }
        catch (ScopeRefusedException)
        {
            return ApiResults.Forbidden("The query's scope refuses this caller.");
        }
        catch (SqlQueryException exception)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]> { [string.Empty] = [.. exception.Messages] });
        }
        catch (Grove.Data.Queries.Structured.StructuredQueryException exception)
        {
            return ApiProblems.Validation(string.Empty, exception.Message);
        }
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess, [FromQuery] string? search = null)
    {
        var access = await requestAccess.AuthorizeAsync(user, Permissions.ManageQueries);
        if (access is null) return TypedResults.Forbid();

        var queries = await access.GetRequiredService<IQueryManager>().ListQueriesAsync(new QueryContext { Name = search });
        var sources = access.GetRequiredService<IEnumerable<IQuerySource>>()
            .Select(source => source.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return TypedResults.Ok(new QueryCatalog(queries.OrderBy(query => query.Name, StringComparer.OrdinalIgnoreCase).Select(QuerySummary.From).ToArray(), sources));
    }

    private static async Task<IResult> CreateAsync([FromBody] QueryWrite write, ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess)
    {
        var access = await requestAccess.AuthorizeAsync(user, Permissions.ManageQueries);
        if (access is null) return TypedResults.Forbid();

        var manager = access.GetRequiredService<IQueryManager>();
        if (await manager.GetQueryAsync(write.Name) is not null) return ApiResults.Conflict("A query with that name already exists.");

        var query = await manager.NewAsync(write.Source);
        if (query is null) return ApiResults.BadRequest("The selected query source is not available for this tenant.");

        Apply(query, write);
        await manager.UpdateAsync(query);
        return TypedResults.Ok(QuerySummary.From(query));
    }

    private static async Task<IResult> UpdateAsync([FromRoute] string name, [FromBody] QueryWrite write, ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess)
    {
        var access = await requestAccess.AuthorizeAsync(user, Permissions.ManageQueries);
        if (access is null) return TypedResults.Forbid();

        var manager = access.GetRequiredService<IQueryManager>();
        var query = await manager.GetQueryAsync(name);
        if (query is null) return TypedResults.NotFound();
        if (!string.Equals(name, write.Name, StringComparison.OrdinalIgnoreCase) && await manager.GetQueryAsync(write.Name) is not null) return ApiResults.Conflict("A query with that name already exists.");
        if (!access.GetRequiredService<IEnumerable<IQuerySource>>().Any(source => string.Equals(source.Name, write.Source, StringComparison.OrdinalIgnoreCase))) return ApiResults.BadRequest("The selected query source is not available for this tenant.");

        Apply(query, write);
        if (!string.Equals(name, write.Name, StringComparison.OrdinalIgnoreCase)) await manager.DeleteQueryAsync(name);
        await manager.UpdateAsync(query);
        return TypedResults.Ok(QuerySummary.From(query));
    }

    private static async Task<IResult> DeleteAsync([FromRoute] string name, ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess)
    {
        var access = await requestAccess.AuthorizeAsync(user, Permissions.ManageQueries);
        if (access is null) return TypedResults.Forbid();
        return await access.GetRequiredService<IQueryManager>().DeleteQueryAsync(name) ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static async Task<IResult> DeleteManyAsync([FromBody] QueryNames request, ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess)
    {
        var access = await requestAccess.AuthorizeAsync(user, Permissions.ManageQueries);
        if (access is null) return TypedResults.Forbid();
        var names = request.Names?.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        if (names.Length == 0) return ApiResults.BadRequest("Select at least one query.");
        return await access.GetRequiredService<IQueryManager>().DeleteQueryAsync(names) ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    /// <summary>
    /// Runs an ad-hoc SQL template (the admin's SQL runner): SELECT only, scoped to the caller,
    /// paged and capped like a saved Sql query, never committed.
    /// </summary>
    private static async Task<IResult> RunSqlAsync([FromBody] SqlRunRequest request, ClaimsPrincipal user, [FromServices] IRequestAccess requestAccess, CancellationToken cancellationToken)
    {
        var access = await requestAccess.AuthorizeAsync(user, QueriesPermissions.ManageSqlQueries);
        if (access is null) return TypedResults.Forbid();
        if (access.GetService<SqlQueryRunner>() is not { } runner) return ApiResults.NotFound("The SQL queries feature is not enabled for this tenant.");
        if (string.IsNullOrWhiteSpace(request.Sql)) return TypedResults.Ok(SqlRun.Failed("Enter a query to run."));

        var parameters = request.Parameters is null
            ? new Dictionary<string, object>()
            : JConvert.DeserializeObject<Dictionary<string, object>>(request.Parameters.ToJsonString()) ?? [];
        var run = await runner.RunAsync(request.Sql, parameters, cancellationToken);

        var rows = new JsonArray(run.Rows.Select(row => JsonSerializer.SerializeToNode(row, row.GetType(), JOptions.Default)).ToArray());
        var columns = run.Columns.Count > 0
            ? run.Columns.Select(column => column.Name).ToArray()
            : rows.OfType<JsonObject>().SelectMany(row => row.Select(pair => pair.Key)).Distinct(StringComparer.Ordinal).ToArray();

        return TypedResults.Ok(new SqlRun(
            run.Errors.ToArray(),
            run.RawSql,
            JsonSerializer.SerializeToNode(run.Parameters, JOptions.Default) as JsonObject ?? [],
            columns,
            rows,
            run.Elapsed.TotalMilliseconds,
            run.FactoryName));
    }

    private static void Apply(Query query, QueryWrite write)
    {
        query.Name = write.Name.Trim();
        query.Source = write.Source.Trim();
        query.Schema = write.Schema?.Trim();
        query.ReturnContentItems = write.ReturnContentItems;
        query.Properties = write.Properties?.DeepClone() as JsonObject ?? [];
    }
}

/// <summary>A saved query's run: the rows, their columns, the total when the source knows it, the next page's token.</summary>
public sealed record QueryRunResult(IEnumerable<object> Items, QueryRunColumn[] Columns, long? Total, string? NextPageToken);

public sealed record QueryRunColumn(string Name, string Type);

public sealed record QueryCatalog(QuerySummary[] Queries, string[] Sources);

public sealed record QuerySummary(string Name, string Source, string? Schema, bool ReturnContentItems, JsonObject Properties)
{
    public static QuerySummary From(Query query) => new(query.Name, query.Source, query.Schema, query.ReturnContentItems, query.Properties.DeepClone() as JsonObject ?? []);
}

public sealed record QueryWrite([property: NotEmpty] string Name, [property: NotEmpty] string Source, string? Schema, bool ReturnContentItems, JsonObject? Properties);

public sealed record QueryNames([property: NotEmpty] string[]? Names);

public sealed record SqlRunRequest(string Sql, JsonObject? Parameters);

public sealed record SqlRun(string[] Errors, string? RawSql, JsonObject Parameters, string[] Columns, JsonArray Rows, double ElapsedMilliseconds, string? FactoryName)
{
    public static SqlRun Failed(string message) => new([message], null, [], [], [], 0, null);
}
