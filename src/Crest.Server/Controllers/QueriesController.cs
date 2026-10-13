using System.Text.Json.Nodes;
using Crest.Services;
using Microsoft.AspNetCore.Mvc;
using Crest.Queries;
using QueriesPermissions = Crest.Queries.Permissions;
using Crest.ViewModels;
using Crest.Queries.Sql;
using System.Text.Json;
using SqlRun = Crest.AdminTheme.Api.SqlRun;
using SqlRunRequest = Crest.AdminTheme.Api.SqlRunRequest;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/queries")]
public sealed class QueriesController(IRequestAccess requestAccess) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<QueryCatalog>> ListAsync([FromQuery] string? search = null)
    {
        var access = await requestAccess.AuthorizeAsync(User, QueriesPermissions.ManageQueries);
        if (access is null) return Forbid();

        var queries = await access.GetRequiredService<IQueryManager>().ListQueriesAsync(new QueryContext { Name = search });
        var sources = access.GetRequiredService<IEnumerable<IQuerySource>>()
            .Select(source => source.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(new QueryCatalog(queries.OrderBy(query => query.Name, StringComparer.OrdinalIgnoreCase).Select(CrestQuery.From).ToArray(), sources));
    }

    [HttpPost]
    public async Task<ActionResult<CrestQuery>> CreateAsync([FromBody] QueryWrite write)
    {
        var access = await requestAccess.AuthorizeAsync(User, QueriesPermissions.ManageQueries);
        if (access is null) return Forbid();
        if (string.IsNullOrWhiteSpace(write.Name) || string.IsNullOrWhiteSpace(write.Source)) return BadRequest("A query name and source are required.");

        var manager = access.GetRequiredService<IQueryManager>();
        if (await manager.GetQueryAsync(write.Name) is not null) return Conflict("A query with that name already exists.");

        var query = await manager.NewAsync(write.Source);
        if (query is null) return BadRequest("The selected query source is not available for this tenant.");

        Apply(query, write);
        await manager.UpdateAsync(query);
        return Ok(CrestQuery.From(query));
    }

    [HttpPut("{name}")]
    public async Task<ActionResult<CrestQuery>> UpdateAsync(string name, [FromBody] QueryWrite write)
    {
        var access = await requestAccess.AuthorizeAsync(User, QueriesPermissions.ManageQueries);
        if (access is null) return Forbid();
        if (string.IsNullOrWhiteSpace(write.Name) || string.IsNullOrWhiteSpace(write.Source)) return BadRequest("A query name and source are required.");

        var manager = access.GetRequiredService<IQueryManager>();
        var query = await manager.GetQueryAsync(name);
        if (query is null) return NotFound();
        if (!string.Equals(name, write.Name, StringComparison.OrdinalIgnoreCase) && await manager.GetQueryAsync(write.Name) is not null) return Conflict("A query with that name already exists.");
        if (!access.GetRequiredService<IEnumerable<IQuerySource>>().Any(source => string.Equals(source.Name, write.Source, StringComparison.OrdinalIgnoreCase))) return BadRequest("The selected query source is not available for this tenant.");

        Apply(query, write);
        if (!string.Equals(name, write.Name, StringComparison.OrdinalIgnoreCase)) await manager.DeleteQueryAsync(name);
        await manager.UpdateAsync(query);
        return Ok(CrestQuery.From(query));
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteAsync(string name)
    {
        var access = await requestAccess.AuthorizeAsync(User, QueriesPermissions.ManageQueries);
        if (access is null) return Forbid();
        return await access.GetRequiredService<IQueryManager>().DeleteQueryAsync(name) ? NoContent() : NotFound();
    }

    [HttpPost("delete")]
    public async Task<IActionResult> DeleteManyAsync([FromBody] QueryNames request)
    {
        var access = await requestAccess.AuthorizeAsync(User, QueriesPermissions.ManageQueries);
        if (access is null) return Forbid();
        var names = request.Names?.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        if (names.Length == 0) return BadRequest("Select at least one query.");
        return await access.GetRequiredService<IQueryManager>().DeleteQueryAsync(names) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Runs an ad-hoc SQL template (the admin's SQL runner): SELECT only, scoped to the caller,
    /// paged and capped like a saved Sql query, never committed.
    /// </summary>
    [HttpPost("sql")]
    public async Task<ActionResult<SqlRun>> RunSqlAsync([FromBody] SqlRunRequest request)
    {
        var access = await requestAccess.AuthorizeAsync(User, Crest.Queries.QueriesPermissions.ManageSqlQueries);
        if (access is null) return Forbid();
        if (access.GetService<SqlQueryRunner>() is not { } runner) return NotFound("The SQL queries feature is not enabled for this tenant.");
        if (string.IsNullOrWhiteSpace(request.Sql)) return Ok(SqlRun.Failed("Enter a query to run."));

        var parameters = request.Parameters is null
            ? new Dictionary<string, object>()
            : JConvert.DeserializeObject<Dictionary<string, object>>(request.Parameters.ToJsonString()) ?? [];
        var run = await runner.RunAsync(request.Sql, parameters, HttpContext.RequestAborted);

        var rows = new JsonArray(run.Rows.Select(row => JsonSerializer.SerializeToNode(row, row.GetType(), JOptions.Default)).ToArray());
        var columns = run.Columns.Count > 0
            ? run.Columns.Select(column => column.Name).ToArray()
            : rows.OfType<JsonObject>().SelectMany(row => row.Select(pair => pair.Key)).Distinct(StringComparer.Ordinal).ToArray();

        return Ok(new SqlRun(
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
