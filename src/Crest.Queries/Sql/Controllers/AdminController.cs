using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Crest.Access;
using Crest.Admin;
using Crest.Modules;
using Crest.Queries.Sql.ViewModels;
using YesSql;

namespace Crest.Queries.Sql.Controllers;

[Feature("Crest.Queries.Sql")]
public sealed class AdminController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IStore _store;
    private readonly ICallerContextAccessor _callerAccessor;
    private readonly IScopeSetProvider _scopeSetProvider;

    internal readonly IStringLocalizer S;

    public AdminController(
        IAuthorizationService authorizationService,
        IStore store,
        IServiceProvider serviceProvider,
        IStringLocalizer<AdminController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _store = store;
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

        var dialect = _store.Configuration.SqlDialect;
        var parameters = JConvert.DeserializeObject<Dictionary<string, object>>(model.Parameters);

        try
        {
            var scopes = await CallerScopes.RequireAsync(_callerAccessor, _scopeSetProvider, SqlQuerySource.SourceName, HttpContext.RequestAborted);

            if (SqlParser.TryParse(model.DecodedQuery, _store.Configuration.Schema, dialect, _store.Configuration.TablePrefix, parameters, scopes, out var rawQuery, out var messages))
            {
                model.RawSql = rawQuery;
                model.Parameters = JConvert.SerializeObject(parameters, JOptions.Indented);

                try
                {
                    await using var connection = _store.Configuration.ConnectionFactory.CreateConnection();
                    await connection.OpenAsync();
                    model.Documents = await connection.QueryAsync(rawQuery, parameters);
                }
                catch (Exception e)
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
