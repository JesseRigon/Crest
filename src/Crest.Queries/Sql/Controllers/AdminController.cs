using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Crest.Access;
using Crest.Admin;
using Crest.Entities;
using Crest.Modules;
using Crest.Queries.Sql.Models;
using Crest.Queries.Sql.ViewModels;
using Crest.Queries.Structured;
using YesSql;

namespace Crest.Queries.Sql.Controllers;

[Feature("Crest.Queries.Sql")]
public sealed class AdminController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IStore _store;
    private readonly IQueryManager _queryManager;
    private readonly ICallerContextAccessor _callerAccessor;
    private readonly IScopeSetProvider _scopeSetProvider;

    internal readonly IStringLocalizer S;

    public AdminController(
        IAuthorizationService authorizationService,
        IStore store,
        IQueryManager queryManager,
        IServiceProvider serviceProvider,
        IStringLocalizer<AdminController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _store = store;
        _queryManager = queryManager;
        _callerAccessor = serviceProvider.GetService(typeof(ICallerContextAccessor)) as ICallerContextAccessor;
        _scopeSetProvider = serviceProvider.GetService(typeof(IScopeSetProvider)) as IScopeSetProvider;
        S = stringLocalizer;
    }

    [Admin("Queries/Sql/Query", "QueriesRunSql")]
    public Task<IActionResult> Query(string query)
    {
        query = string.IsNullOrWhiteSpace(query)
            ? ""
            : Base64.FromUTF8Base64String(query);

        return Query(new AdminQueryViewModel
        {
            DecodedQuery = query,
            FactoryName = _store.Configuration.ConnectionFactory.GetType().FullName,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Query(AdminQueryViewModel model)
    {
        model.FactoryName = _store.Configuration.ConnectionFactory.GetType().FullName;

        if (!await _authorizationService.AuthorizeAsync(User, QueriesPermissions.ManageSqlQueries))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(model.DecodedQuery))
        {
            return View(model);
        }

        if (string.IsNullOrEmpty(model.Parameters))
        {
            model.Parameters = "{ }";
        }

        var validationMessages = SqlParser.Validate(model.DecodedQuery);

        if (validationMessages.Count > 0)
        {
            foreach (var message in validationMessages)
            {
                ModelState.AddModelError(nameof(model.DecodedQuery), message);
            }

            return View(model);
        }

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var parameters = new Dictionary<string, object>(JConvert.DeserializeObject<Dictionary<string, object>>(model.Parameters) ?? [], StringComparer.OrdinalIgnoreCase);

        try
        {
            var scopes = await CallerScopes.RequireAsync(_callerAccessor, _scopeSetProvider, SqlQuerySource.SourceName, HttpContext.RequestAborted);

            // Parsed here only to show the statement and the defaulted parameters; it runs
            // through the query pipeline like any saved Sql query: scoped, paged and capped,
            // in a transaction that is never committed.
            var shown = new Dictionary<string, object>(parameters, StringComparer.OrdinalIgnoreCase);

            if (SqlParser.TryParsePage(model.DecodedQuery, _store.Configuration.Schema, _store.Configuration.SqlDialect, _store.Configuration.TablePrefix, shown, scopes, out var pageQuery, out _, out var messages))
            {
                shown.Remove(SqlParser.SkipParameter);
                shown.Remove(SqlParser.TakeParameter);
                model.RawSql = pageQuery;
                model.Parameters = JConvert.SerializeObject(shown.Where(pair => !pair.Key.StartsWith("__scope", StringComparison.Ordinal)).ToDictionary(), JOptions.Indented);

                var query = new Query { Name = SqlQuerySource.SourceName, Source = SqlQuerySource.SourceName };
                query.Put(new SqlQueryMetadata { Template = model.DecodedQuery });

                try
                {
                    var results = await _queryManager.ExecuteQueryAsync(query, new QueryRequest
                    {
                        Parameters = parameters,
                        PageSize = StructuredQueryCompiler.MaxPageSize,
                        CancellationToken = HttpContext.RequestAborted,
                    });

                    model.Documents = results.Items.ToList();
                }
                catch (Exception e) when (e is not ScopeRefusedException)
                {
                    ModelState.AddModelError("", S["An error occurred while executing the SQL query: {0}", e.Message]);
                }
            }
            else
            {
                foreach (var message in messages)
                {
                    ModelState.AddModelError(nameof(model.DecodedQuery), message);
                }
            }
        }
        catch (ScopeRefusedException e)
        {
            ModelState.AddModelError(nameof(model.DecodedQuery), S["The query was refused: {0}", e.Message]);
        }

        model.Elapsed = stopwatch.Elapsed;

        return View(model);
    }
}
