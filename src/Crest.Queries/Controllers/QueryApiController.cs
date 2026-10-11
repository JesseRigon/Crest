using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Crest.Access;

namespace Crest.Queries.Controllers;

[Route("api/queries")]
[ApiController]
[Authorize]
[IgnoreAntiforgeryToken]
[AllowAnonymous]
public sealed class QueryApiController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IQueryManager _queryManager;

    public QueryApiController(
        IAuthorizationService authorizationService,
        IQueryManager queryManager
        )
    {
        _authorizationService = authorizationService;
        _queryManager = queryManager;
    }

    [HttpGet]
    [Route("{name}")]
    [EndpointName("ApiExecuteQueryGet")]
    public Task<IActionResult> QueryGet(string name, string parameters)
        => ExecuteQueryAsync(name, parameters);

    [HttpPost]
    [Route("{name}")]
    [EndpointName("ApiExecuteQueryPost")]
    public Task<IActionResult> QueryPost(string name, string parameters)
        => ExecuteQueryAsync(name, parameters);

    private async Task<IActionResult> ExecuteQueryAsync(
        string name,
        string parameters)
    {
        var query = await _queryManager.GetQueryAsync(name);

        if (query == null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, Permissions.CreatePermissionForQuery(query.Name)))
        {
            // Intentionally not returning Unauthorized as it is not usable from APIs and would
            // expose the existence of a named query (not a concern per se).
            return NotFound();
        }

        if (Request.Method == HttpMethods.Post && string.IsNullOrEmpty(parameters))
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            parameters = await reader.ReadToEndAsync();
        }

        var queryParameters = !string.IsNullOrEmpty(parameters) ?
            JConvert.DeserializeObject<Dictionary<string, object>>(parameters)
            : [];

        var request = new QueryRequest
        {
            Parameters = queryParameters,
            PageToken = Request.Query["pageToken"].FirstOrDefault(),
            PageSize = int.TryParse(Request.Query["pageSize"].FirstOrDefault(), out var pageSize) ? pageSize : null,
            CancellationToken = HttpContext.RequestAborted,
        };

        try
        {
            var result = await _queryManager.ExecuteQueryAsync(query, request);

            return new ObjectResult(new
            {
                result.Items,
                Columns = result.Columns.Select(column => new { column.Name, Type = column.Type.Name }),
                result.Total,
                result.NextPageToken,
            });
        }
        catch (ScopeRefusedException)
        {
            return Forbid();
        }
    }
}
