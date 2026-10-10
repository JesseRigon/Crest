using System.Collections;
using Microsoft.Extensions.DependencyInjection;
using Crest;
using Crest.Queries;

#pragma warning disable CA1050 // Declare types in namespaces
public static class QueryPlatformRazorHelperExtensions
#pragma warning restore CA1050 // Declare types in namespaces
{
    public static Task<IEnumerable> QueryAsync(this IPlatformHelper platformHelper, string queryName)
    {
        return QueryAsync(platformHelper, queryName, new Dictionary<string, object>());
    }

    public static async Task<IEnumerable> QueryAsync(this IPlatformHelper platformHelper, string queryName, IDictionary<string, object> parameters)
    {
        var queryManager = platformHelper.HttpContext.RequestServices.GetService<IQueryManager>();

        var query = await queryManager.GetQueryAsync(queryName);

        if (query == null)
        {
            return null;
        }

        var result = await queryManager.ExecuteQueryAsync(query, QueryRequest.Of(parameters, platformHelper.HttpContext.RequestAborted));

        return result.Items;
    }

    public static async Task<IQueryResults> QueryResultsAsync(this IPlatformHelper platformHelper, string queryName, IDictionary<string, object> parameters)
    {
        var queryManager = platformHelper.HttpContext.RequestServices.GetService<IQueryManager>();

        var query = await queryManager.GetQueryAsync(queryName);

        if (query == null)
        {
            return null;
        }

        var result = await queryManager.ExecuteQueryAsync(query, QueryRequest.Of(parameters, platformHelper.HttpContext.RequestAborted));

        return result;
    }
}
